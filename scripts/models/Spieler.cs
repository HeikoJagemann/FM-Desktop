namespace FMDesktop.Models;

public class Spieler
{
    public long   Id                   { get; set; }
    public string Name                 { get; set; } = "";
    public int    Alter                { get; set; }
    public string Nationalitaet        { get; set; } = "";
    public string Position             { get; set; } = "";
    public string Kader                { get; set; } = "";
    public int    Staerke              { get; set; }
    public int    Talent               { get; set; }
    public int    Wert                 { get; set; }
    // Technisch
    public int    Pass                 { get; set; }
    public int    Ballkontrolle        { get; set; }
    public int    Schusstechnik        { get; set; }
    public int    Schussstaerke        { get; set; }
    public int    Schnelligkeit        { get; set; }
    public int    Ausdauer             { get; set; }
    public int    Stellungsspiel       { get; set; }
    public int    Entscheidungen       { get; set; }
    public int    Kopfball             { get; set; }
    public int    Zweikampf            { get; set; }
    public int    Dribbling            { get; set; }
    public int    LinkerFuss           { get; set; }
    public int    RechterFuss          { get; set; }
    public int    Fuehrungsqualitaet   { get; set; }
    public int    Disziplin            { get; set; }
    // Torwart
    public int    TalentTW             { get; set; }
    public int    Strafraumbeherrschung { get; set; }
    public int    Fangsicherheit        { get; set; }
    public int    Reflexe              { get; set; }
}
