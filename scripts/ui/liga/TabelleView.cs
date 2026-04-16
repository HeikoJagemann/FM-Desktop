using Godot;
using System.Collections.Generic;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Liga;

public partial class TabelleView : Control
{
    private Tree  _tree        = null!;
    private Label _statusLabel = null!;

    private static readonly string[] Spalten = { "#", "Verein", "Sp", "G", "U", "N", "Tore", "+/-", "Pkt" };

    public override async void _Ready()
    {
        BuildUI();
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

        header.AddChild(FmTheme.MakeLabel("🏆  Tabelle –", 20, FmTheme.TextPrimary));
        header.AddChild(FmTheme.MakeLabel(GameState.Instance.LigaName, 20, FmTheme.Accent));

        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_statusLabel);

        _tree = new Tree
        {
            Columns             = Spalten.Length,
            ColumnTitlesVisible = true,
            HideRoot            = true,
            SelectMode          = Tree.SelectModeEnum.Row,
            SizeFlagsVertical   = SizeFlags.ExpandFill,
        };
        for (int i = 0; i < Spalten.Length; i++)
        {
            _tree.SetColumnTitle(i, Spalten[i]);
            _tree.SetColumnExpand(i, i == 1); // Verein-Spalte expandiert
        }
        vbox.AddChild(_tree);
    }

    private async System.Threading.Tasks.Task LadeTabelle()
    {
        var ligaId  = GameState.Instance.LigaId;
        var eintraege = await ApiClient.GetAsync<List<TabellenEintrag>>($"liga/{ligaId}/tabelle");

        if (eintraege == null) { _statusLabel.Text = "Fehler beim Laden."; return; }
        _statusLabel.Text = $"{eintraege.Count} Vereine";

        _tree.Clear();
        var root = _tree.CreateItem();

        foreach (var e in eintraege)
        {
            var item = _tree.CreateItem(root);
            item.SetText(0, e.Platz.ToString());
            item.SetText(1, e.Verein?.Name ?? "");
            item.SetText(2, e.Spiele.ToString());
            item.SetText(3, e.Siege.ToString());
            item.SetText(4, e.Unentschieden.ToString());
            item.SetText(5, e.Niederlagen.ToString());
            item.SetText(6, $"{e.Tore}:{e.Gegentore}");
            item.SetText(7, (e.Tordifferenz >= 0 ? "+" : "") + e.Tordifferenz);
            item.SetText(8, e.Punkte.ToString());

            // Eigenen Verein hervorheben
            if (e.Verein?.Id == GameState.Instance.VereinId)
            {
                for (int c = 0; c < Spalten.Length; c++)
                    item.SetCustomColor(c, FmTheme.Accent);
            }
            // Aufstiegsplätze (Platz 1-2) grün, Abstiegsplätze (letzte 2) rot
            else if (e.Platz <= 2)
                item.SetCustomColor(0, FmTheme.Success);
            else if (eintraege.Count - e.Platz < 2)
                item.SetCustomColor(0, FmTheme.Danger);
        }
    }
}
