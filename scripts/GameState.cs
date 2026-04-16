using Godot;

namespace FMDesktop;

/// <summary>Autoload-Singleton – hält den laufenden Spielstand.</summary>
public partial class GameState : Node
{
    public static GameState Instance { get; private set; } = null!;

    public long   VereinId   { get; private set; }
    public string VereinName { get; private set; } = "";
    public long   LigaId     { get; private set; }
    public string LigaName   { get; private set; } = "";

    public override void _Ready()
    {
        Instance = this;
    }

    public void SetVerein(long vereinId, string vereinName, long ligaId, string ligaName)
    {
        VereinId   = vereinId;
        VereinName = vereinName;
        LigaId     = ligaId;
        LigaName   = ligaName;
    }
}
