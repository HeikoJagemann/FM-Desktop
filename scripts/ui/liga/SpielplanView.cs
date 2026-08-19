using Godot;
using System.Collections.Generic;
using System.Linq;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Liga;

public partial class SpielplanView : Control
{
    private Tree  _tree        = null!;
    private Label _statusLabel = null!;

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

        _tree = new Tree
        {
            Columns             = 4,
            ColumnTitlesVisible = true,
            HideRoot            = true,
            SizeFlagsVertical   = SizeFlags.ExpandFill,
            SelectMode          = Tree.SelectModeEnum.Row,
            AllowRmbSelect      = true,
        };
        _tree.ItemMouseSelected += (position, mouseButtonIndex) => OnItemMouseSelected(mouseButtonIndex);
        _tree.SetColumnTitle(0, "Heim");
        _tree.SetColumnTitle(1, "Ergebnis");
        _tree.SetColumnTitle(2, "Gast");
        _tree.SetColumnTitle(3, "Status");
        _tree.SetColumnExpand(0, true);
        _tree.SetColumnExpand(2, true);
        _tree.SetColumnExpand(1, false);
        _tree.SetColumnExpand(3, false);
        vbox.AddChild(_tree);
    }

    private void OnItemMouseSelected(long mouseButtonIndex)
    {
        if (mouseButtonIndex != (long)MouseButton.Right) return;

        var item = _tree.GetSelected();
        if (item == null) return;

        // Spieltag-Überschriften tragen keine Metadaten.
        var meta = item.GetMetadata(0);
        if (meta.VariantType == Variant.Type.Nil) return;

        var spielId = meta.AsInt64();
        if (spielId <= 0) return;

        SpielKontextmenue.Zeige(this, spielId, item.GetMetadata(1).AsBool());
    }

    private async System.Threading.Tasks.Task LadeSpielplan()
    {
        var ligaId = GameState.Instance.LigaId;
        var spiele = await ApiClient.GetAsync<List<Spiel>>($"liga/{ligaId}/spielplan");

        if (spiele == null) { _statusLabel.Text = "Fehler beim Laden."; return; }

        int gespielt = spiele.Count(s => s.Gespielt);
        _statusLabel.Text = $"{spiele.Count} Spiele · {gespielt} ausgetragen"
                          + "   (Rechtsklick auf ein Spiel öffnet den Spielbericht)";

        _tree.Clear();
        var root = _tree.CreateItem();

        var nachSpieltag = spiele.GroupBy(s => s.Spieltag).OrderBy(g => g.Key).ToList();

        // Den zuletzt gespielten Spieltag aufgeklappt lassen - sonst muss der Nutzer nach jedem
        // simulierten Spieltag erst suchen, wo etwas passiert ist.
        int offenerSpieltag = nachSpieltag
            .Where(g => g.Any(s => s.Gespielt))
            .Select(g => g.Key)
            .DefaultIfEmpty(nachSpieltag.FirstOrDefault()?.Key ?? 0)
            .Max();

        foreach (var gruppe in nachSpieltag)
        {
            // Spieltag-Überschrift
            var header = _tree.CreateItem(root);
            header.SetText(0, $"  Spieltag {gruppe.Key}");
            header.SetCustomColor(0, FmTheme.TextSecondary);
            header.SetSelectable(0, false);
            header.SetSelectable(1, false);
            header.SetSelectable(2, false);
            header.SetSelectable(3, false);
            header.Collapsed = gruppe.Key != offenerSpieltag;

            foreach (var spiel in gruppe)
            {
                var item = _tree.CreateItem(header);
                item.SetMetadata(0, spiel.Id);
                item.SetMetadata(1, spiel.Gespielt);
                item.SetText(0, spiel.HeimVerein?.Name ?? "");
                item.SetText(1, spiel.Ergebnis);
                item.SetText(2, spiel.GastVerein?.Name ?? "");
                item.SetText(3, spiel.Gespielt ? "✓" : "–");

                // Eigene Spiele hervorheben
                bool eigenes = spiel.HeimVerein?.Id == GameState.Instance.VereinId
                            || spiel.GastVerein?.Id  == GameState.Instance.VereinId;
                if (eigenes)
                {
                    item.SetCustomColor(0, FmTheme.Accent);
                    item.SetCustomColor(1, FmTheme.Accent);
                    item.SetCustomColor(2, FmTheme.Accent);
                }
            }
        }
    }
}
