#nullable enable
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Wettbewerbe;

/// <summary>
/// Der DFB-Pokal Runde für Runde, darunter Relegation und Aufstiegsspiele zur 3. Liga, sobald sie
/// angesetzt sind.
/// </summary>
public partial class PokalView : Control
{
    private Label _status = null!;
    private FmGrid<PaarungModel> _pokal = null!;
    private Label _relegationTitel = null!;
    private FmGrid<PaarungModel> _relegation = null!;

    public override async void _Ready()
    {
        BuildUI();
        await Lade();
    }

    private void BuildUI()
    {
        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 10);
        AddChild(vbox);

        vbox.AddChild(FmTheme.MakeTitel("DFB-Pokal"));
        _status = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_status);

        _pokal = new FmGrid<PaarungModel>(PaarungSpalten.Liste)
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Gruppieren        = p => p.Gruppe,
        };
        vbox.AddChild(_pokal);

        _relegationTitel = FmTheme.MakeLabel("Relegation und Aufstiegsspiele", 15, FmTheme.TextPrimary);
        _relegationTitel.Visible = false;
        vbox.AddChild(_relegationTitel);
        _relegation = new FmGrid<PaarungModel>(PaarungSpalten.Liste)
        {
            CustomMinimumSize = new Vector2(0, 160),
            Gruppieren        = p => p.Gruppe,
            Visible           = false,
        };
        vbox.AddChild(_relegation);
    }

    private async Task Lade()
    {
        var pokal = await ApiClient.GetAsync<List<PaarungModel>>("wettbewerb/DFB_POKAL");
        var relegation = await ApiClient.GetAsync<List<PaarungModel>>("wettbewerb/RELEGATION") ?? new();
        var aufstieg = await ApiClient.GetAsync<List<PaarungModel>>("wettbewerb/AUFSTIEGSSPIEL") ?? new();
        if (!IsInstanceValid(this)) return;
        if (pokal == null) { _status.Text = "Fehler beim Laden."; return; }

        long eigener = GameState.Instance.VereinId;
        var eigene = pokal.Where(p => p.Betrifft(eigener)).ToList();
        _status.Text = eigene.Count == 0 ? "Dein Verein ist in diesem Jahr nicht dabei."
            : eigene.Last().SiegerId == null ? $"Dein Verein steht in der Runde \"{eigene.Last().Stufe}\"."
            : eigene.Last().SiegerId == eigener ? $"Dein Verein ist weiter - nächste Runde folgt nach der Auslosung."
            : $"Dein Verein ist in der Runde \"{eigene.Last().Stufe}\" ausgeschieden.";
        _pokal.Zeige(pokal);
        string? letzte = pokal.Select(p => p.Gruppe).LastOrDefault();
        if (letzte != null) _pokal.NurGruppeOffen(letzte);

        var unten = relegation.Concat(aufstieg).ToList();
        _relegationTitel.Visible = _relegation.Visible = unten.Count > 0;
        _relegation.Zeige(unten);
    }
}
