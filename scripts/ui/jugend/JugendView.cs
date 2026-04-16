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
        int[] minBreiten  = { 120,  48,  64,  90,  52,  90 };
        bool[] expandiert = { true, false, false, false, false, true };
        int[] ratios      = {    3,    0,    0,    0,    0,   1 };

        for (int i = 0; i < Spalten.Length; i++)
        {
            tree.SetColumnTitle(i, Spalten[i]);
            tree.SetColumnCustomMinimumWidth(i, minBreiten[i]);
            tree.SetColumnExpand(i, expandiert[i]);
            if (expandiert[i])
                tree.SetColumnExpandRatio(i, ratios[i]);
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

    private static string TalentSterne(int talent)
    {
        int sterne = talent switch
        {
            >= 80 => 5,
            >= 65 => 4,
            >= 50 => 3,
            >= 35 => 2,
            _     => 1,
        };
        return new string('★', sterne) + new string('☆', 5 - sterne);
    }

    private static void FuelleBaum(Tree tree, List<Spieler> spieler)
    {
        tree.Clear();
        var root = tree.CreateItem();
        foreach (var s in spieler
            .OrderBy(x => PositionsPrioritaet(x.Position))
            .ThenByDescending(x => x.Talent))
        {
            var item = tree.CreateItem(root);
            item.SetText(0, s.Name);
            item.SetText(1, s.Position);
            item.SetText(2, s.Staerke.ToString());
            item.SetText(3, TalentSterne(s.Talent));
            item.SetText(4, s.Alter.ToString());
            item.SetText(5, s.Nationalitaet);

            var talentFarbe = s.Talent >= 80 ? FmTheme.Gold
                            : s.Talent >= 65 ? FmTheme.Success
                            : FmTheme.TextSecondary;
            item.SetCustomColor(3, talentFarbe);
        }
    }

    private static int PositionsPrioritaet(string pos) => pos switch
    {
        "TW"                                    => 0,
        "IV" or "LV" or "RV"                   => 1,
        "DM" or "ZM" or "LM" or "RM" or "OM"  => 2,
        "LA" or "RA" or "HS" or "ST"           => 3,
        _                                       => 9,
    };
}
