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
    private HBoxContainer           _header      = null!;

    public override async void _Ready()
    {
        BuildUI();
        var auswahl = await LigaAuswahl.Baue(async _ => await LadeTabelle());
        if (!IsInstanceValid(this)) return;
        auswahl.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _header.AddChild(auswahl);
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
        _header = header;

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
        var ligaId    = LigaAuswahl.Aktuell;
        var eintraege = await ApiClient.GetAsync<List<TabellenEintrag>>($"liga/{ligaId}/tabelle");

        if (eintraege == null) { _statusLabel.Text = "Fehler beim Laden."; return; }
        _statusLabel.Text = $"{eintraege.Count} Vereine";

        _grid.Zeilenfarbe = TabellenSpalten.Zeilenfarbe(
            GameState.Instance.VereinId, eintraege.Count);
        _grid.Zeige(eintraege);
    }
}
