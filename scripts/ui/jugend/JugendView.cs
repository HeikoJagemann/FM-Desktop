using Godot;
using System.Collections.Generic;
using System.Linq;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Jugend;

public partial class JugendView : Control
{
    private TabContainer  _tabs        = null!;
    private Label         _statusLabel = null!;
    private List<Spieler> _alleSpieler = new();

    /// <summary>Je Jugendmannschaft ein Grid, geschlüsselt nach Kadertyp ("JugendA").</summary>
    private readonly Dictionary<string, FmGrid<Spieler>> _grids = new();

    public override async void _Ready()
    {
        BuildUI();
        // Ein hochgezogenes Talent verschwindet aus der Jugend - also neu laden.
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

        vbox.AddChild(FmTheme.MakeLabel("👦  Jugendabteilung", 20, FmTheme.TextPrimary));

        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_statusLabel);

        _tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        vbox.AddChild(_tabs);

        BaueGrid("Jugend A");
        BaueGrid("Jugend B");
        BaueGrid("Jugend C");
    }

    /// <summary>
    /// Dieselben Spalten wie im Profikader, nur mit der rohen Stärke: In der Jugend zählt der
    /// positionsunabhängige Wert, weil die Position noch nicht feststeht.
    /// </summary>
    private void BaueGrid(string tabName)
    {
        var grid = new FmGrid<Spieler>(SpielerSpalten.Jugendliste)
        {
            Name               = tabName,
            Zeilenfarbe        = SpielerSpalten.Zeilenfarbe,
            ZebraNeuBei        = s => s.Gruppe,
            Standardsortierung = SpielerSpalten.NachTalent,
        };
        grid.Rechtsklick += spieler => SpielerKontextmenue.Zeige(this, spieler);
        _tabs.AddChild(grid);
        _grids[tabName.Replace(" ", "")] = grid;
    }

    private async System.Threading.Tasks.Task LadeSpieler()
    {
        var id   = GameState.Instance.VereinId;
        var alle = await ApiClient.GetAsync<List<Spieler>>($"verein/{id}/spieler");

        if (alle == null) { _statusLabel.Text = "Fehler beim Laden."; return; }

        _statusLabel.Text = $"{alle.Count(s => s.Kader.StartsWith("Jugend"))} Jugendspieler geladen";
        _alleSpieler = alle;

        foreach (var (kaderTyp, grid) in _grids)
        {
            grid.Zeige(alle.Where(s => s.Kader == kaderTyp));
        }
    }
}
