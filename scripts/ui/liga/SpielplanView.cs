using Godot;
using System.Collections.Generic;
using System.Linq;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Liga;

public partial class SpielplanView : Control
{
    private FmGrid<Spiel> _grid        = null!;
    private Label         _statusLabel = null!;

    public override async void _Ready()
    {
        BuildUI();
        await LadeSpielplan();
    }

    private void BuildUI()
    {
        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 10);
        AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel("📅  Spielplan", 20, FmTheme.TextPrimary));

        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_statusLabel);

        _grid = new FmGrid<Spiel>(SpielplanSpalten.Spielplan)
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Gruppieren        = s => $"Spieltag {s.Spieltag}",
        };
        _grid.Rechtsklick += spiel => SpielKontextmenue.Zeige(this, spiel.Id, spiel.Gespielt);
        vbox.AddChild(_grid);
    }

    private async System.Threading.Tasks.Task LadeSpielplan()
    {
        var ligaId = GameState.Instance.LigaId;
        var spiele = await ApiClient.GetAsync<List<Spiel>>($"liga/{ligaId}/spielplan");

        if (spiele == null) { _statusLabel.Text = "Fehler beim Laden."; return; }

        int gespielt = spiele.Count(s => s.Gespielt);
        _statusLabel.Text = $"{spiele.Count} Spiele · {gespielt} ausgetragen"
                          + "   (Rechtsklick auf ein Spiel öffnet den Spielbericht)";

        _grid.Zeige(spiele.OrderBy(s => s.Spieltag));

        // Den zuletzt gespielten Spieltag aufgeklappt lassen - sonst muss der Nutzer nach jedem
        // simulierten Spieltag erst suchen, wo etwas passiert ist.
        int offener = spiele.Where(s => s.Gespielt)
                            .Select(s => s.Spieltag)
                            .DefaultIfEmpty(spiele.Min(s => s.Spieltag))
                            .Max();
        _grid.NurGruppeOffen($"Spieltag {offener}");
    }
}
