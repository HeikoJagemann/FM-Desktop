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
/// Champions League, Europa League und Conference League: links die Tabelle der Ligaphase - Platz
/// 1 bis 8 im Achtelfinale, 9 bis 24 in den K.-o.-Play-offs -, rechts die Duelle von der
/// Qualifikation bis zum Finale.
/// </summary>
public partial class EuropapokalView : Control
{
    private static readonly string[] Kuerzel = { "CHAMPIONS_LEAGUE", "EUROPA_LEAGUE", "CONFERENCE_LEAGUE" };
    private static readonly string[] Namen = { "Champions League", "Europa League", "Conference League" };

    /// <summary>Für die Sitzung gemerkt - wer in der Europa League spielt, will dort wieder hin.</summary>
    private static int _gewaehlt;

    private Label _status = null!;
    private FmGrid<TabellenEintrag> _tabelle = null!;
    private FmGrid<PaarungModel> _runden = null!;

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

        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 16);
        kopf.AddChild(FmTheme.MakeTitel("Europapokal"));
        var schalter = Segmentschalter.Neu(Namen, _gewaehlt, new Vector2(170, 32), 13);
        schalter.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        schalter.Gewaehlt += async i => { _gewaehlt = i; await Lade(); };
        kopf.AddChild(schalter);
        vbox.AddChild(kopf);

        _status = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_status);

        var spalten = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        spalten.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(spalten);

        var links = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.8f };
        links.AddChild(FmTheme.MakeLabel("Ligaphase", 15, FmTheme.TextPrimary));
        _tabelle = new FmGrid<TabellenEintrag>(TabellenSpalten.Ligatabelle) { SizeFlagsVertical = SizeFlags.ExpandFill };
        _tabelle.Zeilenfarbe = (e, _) => e.Verein?.Id == GameState.Instance.VereinId ? FmTheme.Accent.Darkened(0.6f)
            : e.Platz <= 8 ? FmTheme.Success.Darkened(0.75f)
            : e.Platz <= 24 ? FmTheme.Gold.Darkened(0.8f)
            : FmTheme.BgPanel;
        links.AddChild(_tabelle);
        spalten.AddChild(links);

        var rechts = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rechts.AddChild(FmTheme.MakeLabel("Qualifikation und K.-o.-Runden", 15, FmTheme.TextPrimary));
        _runden = new FmGrid<PaarungModel>(PaarungSpalten.Liste)
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Gruppieren        = p => p.Gruppe,
        };
        rechts.AddChild(_runden);
        spalten.AddChild(rechts);
    }

    private async Task Lade()
    {
        string w = Kuerzel[_gewaehlt];
        var tabelle = await ApiClient.GetAsync<List<TabellenEintrag>>($"wettbewerb/{w}/ligaphase");
        var paarungen = await ApiClient.GetAsync<List<PaarungModel>>($"wettbewerb/{w}");
        if (!IsInstanceValid(this)) return;
        if (tabelle == null || paarungen == null) { _status.Text = "Fehler beim Laden."; return; }

        long eigener = GameState.Instance.VereinId;
        bool dabei = tabelle.Any(e => e.Verein?.Id == eigener) || paarungen.Any(p => p.Betrifft(eigener));
        _status.Text = tabelle.Count == 0
            ? $"{Namen[_gewaehlt]}: Die Ligaphase wird nach den Play-offs Ende August ausgelost."
              + (dabei ? " Dein Verein ist in der Qualifikation dabei." : "")
            : $"{Namen[_gewaehlt]}: 36 Vereine in der Ligaphase" + (dabei ? " - dein Verein ist dabei." : ".");
        _tabelle.Zeige(tabelle);
        _runden.Zeige(paarungen);
        string? letzte = paarungen.Select(p => p.Gruppe).LastOrDefault();
        if (letzte != null) _runden.NurGruppeOffen(letzte);
    }
}
