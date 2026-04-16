using Godot;
using System.Collections.Generic;
using System.Linq;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Mannschaft;

public partial class KaderView : Control
{
    private TabContainer _tabs     = null!;
    private Tree         _profiTree   = null!;
    private Tree         _amateurTree = null!;
    private Label        _statusLabel = null!;

    private static readonly string[] Spalten = { "Name", "Pos", "Stärke", "Talent", "Alter", "Wert (€)", "Nation" };

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

        // Überschrift
        var heading = FmTheme.MakeLabel("👥  Mannschaft – Kader", 20, FmTheme.TextPrimary);
        vbox.AddChild(heading);

        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_statusLabel);

        // Tabs
        _tabs = new TabContainer();
        _tabs.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(_tabs);

        _profiTree   = BaueTree("Profikader");
        _amateurTree = BaueTree("Amaterurkader");
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

        // Spalte: Name, Pos, Stärke, Talent, Alter, Wert, Nation
        int[] minBreiten    = {  120,  48,  64,  90,  52, 110,  90 };
        bool[] expandiert   = { true, false, false, false, false, true, true };
        int[] expandRatios  = {    3,    0,    0,    0,    0,   2,   1 };

        for (int i = 0; i < Spalten.Length; i++)
        {
            tree.SetColumnTitle(i, Spalten[i]);
            tree.SetColumnCustomMinimumWidth(i, minBreiten[i]);
            tree.SetColumnExpand(i, expandiert[i]);
            if (expandiert[i])
                tree.SetColumnExpandRatio(i, expandRatios[i]);
        }

        tree.AddThemeColorOverride("title_button_color", FmTheme.TextSecondary);
        _tabs.AddChild(tree);
        return tree;
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

        FuelleBaum(_profiTree,   alle.Where(s => s.Kader == "Profi").ToList());
        FuelleBaum(_amateurTree, alle.Where(s => s.Kader == "Amateur").ToList());
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

        foreach (var s in spieler.OrderBy(x => PositionsPrioritaet(x.Position)))
        {
            var item = tree.CreateItem(root);
            item.SetText(0, s.Name);
            item.SetText(1, s.Position);
            item.SetText(2, s.Staerke.ToString());
            item.SetText(3, TalentSterne(s.Talent));
            item.SetText(4, s.Alter.ToString());
            item.SetText(5, $"{s.Wert:N0}");
            item.SetText(6, s.Nationalitaet);

            var staerkeFarbe = s.Staerke >= 70 ? FmTheme.Success
                             : s.Staerke >= 50 ? FmTheme.TextPrimary
                             : FmTheme.TextSecondary;
            item.SetCustomColor(2, staerkeFarbe);

            var talentFarbe = s.Talent >= 80 ? FmTheme.Gold
                            : s.Talent >= 65 ? FmTheme.Success
                            : FmTheme.TextSecondary;
            item.SetCustomColor(3, talentFarbe);
        }
    }

    private static int PositionsPrioritaet(string pos) => pos switch
    {
        "TW"                         => 0,
        "IV" or "LV" or "RV"        => 1,
        "DM" or "ZM" or "LM" or "RM" or "OM" => 2,
        "LA" or "RA" or "HS" or "ST" => 3,
        _                             => 9,
    };
}
