using Godot;
using System.Collections.Generic;
using System.Linq;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Mannschaft;

public partial class KaderView : Control
{
    private TabContainer    _tabs        = null!;
    private FmGrid<Spieler> _profiGrid   = null!;
    private FmGrid<Spieler> _amateurGrid = null!;
    private Label           _statusLabel = null!;
    private List<Spieler>   _alleSpieler = new();

    public override async void _Ready()
    {
        BuildUI();
        // Nach einem Kaderwechsel neu laden - der Spieler steht dann in einem anderen Tab.
        SpielerKontextmenue.KaderGeaendert += OnKaderGeaendert;
        await LadeSpieler();
    }

    public override void _ExitTree()
    {
        SpielerKontextmenue.KaderGeaendert -= OnKaderGeaendert;
    }

    private async void OnKaderGeaendert() => await LadeSpieler();

    private void BuildUI()
    {
        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 10);
        AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel("👥  Mannschaft – Kader", 20, FmTheme.TextPrimary));

        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_statusLabel);

        _tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        vbox.AddChild(_tabs);

        _profiGrid   = BaueGrid("Profikader");
        _amateurGrid = BaueGrid("Amateurkader");
    }

    /// <summary>Spalten, Farben, Mouseover und Rechtsklick kommen aus dem gemeinsamen Katalog.</summary>
    private FmGrid<Spieler> BaueGrid(string tabName)
    {
        var grid = new FmGrid<Spieler>(SpielerSpalten.Kaderliste)
        {
            Name               = tabName,
            Zeilenfarbe        = SpielerSpalten.Zeilenfarbe,
            ZebraNeuBei        = s => s.Gruppe,
            Standardsortierung = SpielerSpalten.NachAufstellung,
        };
        grid.Rechtsklick += spieler => SpielerKontextmenue.Zeige(this, spieler);
        _tabs.AddChild(grid);
        return grid;
    }

    private async System.Threading.Tasks.Task LadeSpieler()
    {
        var id = GameState.Instance.VereinId;
        var alle = await ApiClient.GetAsync<List<Spieler>>($"verein/{id}/spieler");

        if (alle == null)
        {
            _statusLabel.Text = "Fehler beim Laden der Spieler.";
            return;
        }

        _statusLabel.Text = $"{alle.Count} Spieler geladen";
        _alleSpieler = alle;

        _profiGrid.Zeige(alle.Where(s => s.Kader == "Profi"));
        _amateurGrid.Zeige(alle.Where(s => s.Kader == "Amateur"));
    }
}
