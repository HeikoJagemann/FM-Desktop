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
    private List<Spieler> _alleSpieler = new();

    private static readonly string[] Spalten = { "Name", "Pos", "Stärken", "Talent", "Alter", "Wert (€)", "Nation" };

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
            AllowRmbSelect      = true,
        };
        tree.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Spalte: Name, Pos, Stärken, Talent, Alter, Wert, Nation
        int[] minBreiten    = {  120,  48, 190,  90,  52, 110,  90 };
        bool[] expandiert   = { true, false, true, false, false, true, true };
        int[] expandRatios  = {    3,    0,    2,    0,    0,   2,   1 };

        for (int i = 0; i < Spalten.Length; i++)
        {
            tree.SetColumnTitle(i, Spalten[i]);
            tree.SetColumnCustomMinimumWidth(i, minBreiten[i]);
            tree.SetColumnExpand(i, expandiert[i]);
            if (expandiert[i])
                tree.SetColumnExpandRatio(i, expandRatios[i]);
        }

        tree.AddThemeColorOverride("title_button_color", FmTheme.TextSecondary);
        tree.ItemMouseSelected += (position, mouseButtonIndex) => OnItemMouseSelected(tree, mouseButtonIndex);
        _tabs.AddChild(tree);
        return tree;
    }

    private void OnItemMouseSelected(Tree tree, long mouseButtonIndex)
    {
        if (mouseButtonIndex != (long)MouseButton.Right) return;

        var item = tree.GetSelected();
        if (item == null) return;
        var id = item.GetMetadata(0).AsInt64();
        var spieler = _alleSpieler.FirstOrDefault(s => s.Id == id);
        if (spieler == null) return;

        SpielerKontextmenue.Zeige(this, spieler);
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

        // Nach Positionsgruppe sortiert, darin die stärkeren Spieler zuerst.
        var sortiert = spieler
            .OrderBy(x => x.Sortierung.Item1)
            .ThenBy(x => x.Sortierung.Item2)
            .ThenBy(x => x.Name)
            .ToList();

        Positionsgruppe? letzte = null;
        bool abgesetzt = false;

        foreach (var s in sortiert)
        {
            if (letzte != s.Gruppe) { abgesetzt = false; letzte = s.Gruppe; }

            var item = tree.CreateItem(root);
            item.SetMetadata(0, s.Id);
            item.SetText(0, s.Name);
            // Nominal zählt nur Torwart oder Feldspieler - angezeigt wird die aus den
            // Fähigkeiten abgeleitete stärkste Position.
            item.SetText(1, s.HauptPosition);
            item.SetText(2, s.Top3PositionenText);
            item.SetTooltipText(2, s.Top3PositionenErklaerung);
            item.SetText(3, TalentSterne(s.Talent));
            item.SetText(4, s.Alter.ToString());
            item.SetText(5, $"{s.Wert:N0}");
            item.SetText(6, s.Nationalitaet);

            var zeilenfarbe = FmTheme.FuerGruppe(s.Gruppe, abgesetzt);
            for (int spalte = 0; spalte < Spalten.Length; spalte++)
                item.SetCustomBgColor(spalte, zeilenfarbe);

            item.SetCustomColor(1, FmTheme.TextFuerGruppe(s.Gruppe));

            var bestStaerke = s.BestPositionStaerke;
            var staerkeFarbe = bestStaerke >= 70 ? FmTheme.Success
                             : bestStaerke >= 50 ? FmTheme.TextPrimary
                             : FmTheme.TextSecondary;
            item.SetCustomColor(2, staerkeFarbe);

            var talentFarbe = s.Talent >= 80 ? FmTheme.Gold
                            : s.Talent >= 65 ? FmTheme.Success
                            : FmTheme.TextSecondary;
            item.SetCustomColor(3, talentFarbe);

            abgesetzt = !abgesetzt;
        }
    }
}
