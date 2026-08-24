using Godot;
using System;
using FMDesktop.Api;

namespace FMDesktop;

/// <summary>Autoload-Singleton – hält den laufenden Spielstand.</summary>
public partial class GameState : Node
{
    public static GameState Instance { get; private set; } = null!;

    public long   VereinId      { get; private set; }
    public string VereinName    { get; private set; } = "";
    public long   LigaId        { get; private set; }
    public string LigaName      { get; private set; } = "";
    public string CurrentSchema { get; private set; } = "db_default";

    /// <summary>
    /// Das "Heute" der Spielwelt. Das Alter eines Spielers zählt danach, nicht nach der Uhr des
    /// Rechners - sonst altert ein Spieler über den Saisonwechsel hinweg nicht.
    /// </summary>
    public DateOnly Spieldatum { get; private set; } = DateOnly.FromDateTime(DateTime.Today);

    public void SetSpieldatum(DateOnly datum) => Spieldatum = datum;

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

    public void SetSchema(string schema)
    {
        CurrentSchema          = schema;
        ApiClient.CurrentSchema = schema;
    }
}
