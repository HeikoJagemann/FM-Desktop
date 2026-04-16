using Godot;
using System.Collections.Generic;
using System.Linq;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Jugend;

public partial class JugendView : Control
{
    private TabContainer _tabs = null!;
    private Label        _statusLabel = null!;

    private static readonly string[] Spalten = { "Name", "Pos", "Stärke", "Talent", "Alter", "Nation" };

    public override async void _Ready()
    {
        BuildUI();
        await LadeSpieler();
    }

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

        BaueTree("Jugend A");
        BaueTree("Jugend B");
        BaueTree("Jugend C");
    }

    private Tree BaueTree(string tabName)
    {
        var tree = new Tree
        {
            Name                = tabName,
            Columns             = Spalten.Length,
            ColumnTitlesVisible = true,
            HideRoot            = true,
            SelectMode          = Tree.SelectModeEnum.Row,
        };
        tree.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        for (int i = 0; i < Spalten.Length; i++)
        {
            tree.SetColumnTitle(i, Spalten[i]);
            tree.SetColumnExpand(i, i == 0);
        }
        _tabs.AddChild(tree);
        return tree;
    }

    private async System.Threading.Tasks.Task LadeSpieler()
    {
        var id   = GameState.Instance.VereinId;
        var alle = await ApiClient.GetAsync<List<Spieler>>($"verein/{id}/spieler");

        if (alle == null) { _statusLabel.Text = "Fehler beim Laden."; return; }

        _statusLabel.Text = $"{alle.Count(s => s.Kader.StartsWith("Jugend"))} Jugendspieler geladen";

        foreach (var tab in _tabs.GetChildren())
        {
            if (tab is not Tree tree) continue;
            var kaderTyp = tab.Name.ToString().Replace(" ", ""); // "JugendA"
            FuelleBaum(tree, alle.Where(s => s.Kader == kaderTyp).ToList());
        }
    }

    private static void FuelleBaum(Tree tree, List<Spieler> spieler)
    {
        tree.Clear();
        var root = tree.CreateItem();
        foreach (var s in spieler.OrderByDescending(x => x.Talent))
        {
            var item = tree.CreateItem(root);
            item.SetText(0, s.Name);
            item.SetText(1, s.Position);
            item.SetText(2, s.Staerke.ToString());
            item.SetText(3, s.Talent.ToString());
            item.SetText(4, s.Alter.ToString());
            item.SetText(5, s.Nationalitaet);

            // Talente hervorheben
            if (s.Talent >= 80)
                item.SetCustomColor(3, FmTheme.Gold);
        }
    }
}
