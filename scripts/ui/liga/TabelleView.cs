using Godot;
using System.Collections.Generic;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Liga;

public partial class TabelleView : Control
{
    private FmGrid<TabellenEintrag> _grid        = null!;
    private Label                   _statusLabel = null!;

    public override async void _Ready()
    {
        BuildUI();
        await LadeTabelle();
    }

    private void BuildUI()
    {
        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 10);
        AddChild(vbox);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(header);

        header.AddChild(FmTheme.MakeTitel("Tabelle"));
        // Bernstein bleibt den Aktionen vorbehalten - die Liga steht ruhig daneben.
        var liga = FmTheme.MakeLabel(GameState.Instance.LigaName, 14, FmTheme.TextSecondary);
        liga.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(liga);

        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_statusLabel);

        _grid = new FmGrid<TabellenEintrag>(TabellenSpalten.Ligatabelle)
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        vbox.AddChild(_grid);
    }

    private async System.Threading.Tasks.Task LadeTabelle()
    {
        var ligaId    = GameState.Instance.LigaId;
        var eintraege = await ApiClient.GetAsync<List<TabellenEintrag>>($"liga/{ligaId}/tabelle");

        if (eintraege == null) { _statusLabel.Text = "Fehler beim Laden."; return; }
        _statusLabel.Text = $"{eintraege.Count} Vereine";

        _grid.Zeilenfarbe = TabellenSpalten.Zeilenfarbe(
            GameState.Instance.VereinId, eintraege.Count);
        _grid.Zeige(eintraege);
    }
}
