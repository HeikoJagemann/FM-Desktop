#nullable enable
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Transfer;

/// <summary>
/// Die Transferliste: Spieler, die ihre Vereine abgeben wollen, zum ausgeschriebenen Preis.
///
/// <para>Wer nicht darauf steht, ist nicht verloren - ihn findet man über das Scouting und wirbt
/// ihn mit einem Angebot ab. Stärke und Talent stehen hier als Spannen: Genau kennt der Verein nur
/// seine eigenen Spieler.</para>
///
/// <para>Gewechselt wird nur im offenen Transferfenster - außerhalb bleibt die Liste sichtbar,
/// aber Angebote sind gesperrt.</para>
/// </summary>
public partial class TransferView : Control
{
    private static readonly string[] Positionen =
        { "Alle", "TW", "IV", "LV", "RV", "DM", "ZM", "LM", "RM", "OM", "RA", "LA", "HS", "ST" };

    private Label _statusLabel = null!;
    private Label _fensterLabel = null!;
    private OptionButton _positionFeld = null!;
    private SpinBox _maxAlterFeld = null!;
    private CheckBox _nurBezahlbar = null!;
    private Button _bietenButton = null!;
    private Button _beobachtenButton = null!;
    private FmGrid<TransferSpieler> _grid = null!;

    private TransferSpieler? _gewaehlt;
    private bool _fensterOffen;

    /// <summary>
    /// Ob die Ansicht noch da ist. Nach jedem Abruf zu prüfen: Während eine Anfrage unterwegs ist,
    /// kann der Nutzer längst in eine andere Ansicht gewechselt sein - dann sind die Labels
    /// freigegeben, und jeder Zugriff wirft eine ObjectDisposedException.
    /// </summary>
    private bool Lebt => IsInstanceValid(this) && !IsQueuedForDeletion();

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

        vbox.AddChild(FmTheme.MakeTitel("Transferliste"));

        _fensterLabel = FmTheme.MakeLabel("", 13, FmTheme.TextSecondary);
        vbox.AddChild(_fensterLabel);

        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_statusLabel);

        vbox.AddChild(BaueFilterleiste());

        _grid = new FmGrid<TransferSpieler>(TransferSpalten.Transferliste)
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _grid.Ausgewaehlt += s => { _gewaehlt = s; AktualisiereKnoepfe(); };
        _grid.Doppelklick += s => ScoutberichtDialog.Zeige(this, s, _fensterOffen, Melde);
        _grid.Rechtsklick += s => TransferAktionen.Zeige(this, s, _fensterOffen, Melde);
        vbox.AddChild(_grid);
    }

    private Control BaueFilterleiste()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());

        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 12, 10);
        panel.AddChild(rand);

        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 12);
        rand.AddChild(zeile);

        zeile.AddChild(FmTheme.MakeLabel("Position:", 13, FmTheme.TextSecondary));
        _positionFeld = new OptionButton { CustomMinimumSize = new Vector2(90, 0) };
        foreach (var p in Positionen) _positionFeld.AddItem(p);
        _positionFeld.ItemSelected += async _ => await Suche();
        zeile.AddChild(_positionFeld);

        zeile.AddChild(FmTheme.MakeLabel("Höchstalter:", 13, FmTheme.TextSecondary));
        _maxAlterFeld = new SpinBox
        {
            MinValue = 0, MaxValue = 40, Step = 1, Value = 0,
            CustomMinimumSize = new Vector2(80, 0),
            TooltipText = "0 = jedes Alter",
        };
        zeile.AddChild(_maxAlterFeld);

        _nurBezahlbar = new CheckBox { Text = "nur bezahlbare", ButtonPressed = true };
        _nurBezahlbar.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
        _nurBezahlbar.TooltipText = "Blendet Spieler aus, deren Preis den Kontostand übersteigt.";
        zeile.AddChild(_nurBezahlbar);

        var suchen = new Button { Text = "Suchen" };
        FmTheme.ApplyButton(suchen, FmTheme.BgPanel);
        suchen.AddThemeColorOverride("font_color", FmTheme.TextPrimary);
        suchen.Pressed += async () => await Suche();
        zeile.AddChild(suchen);

        zeile.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _beobachtenButton = new Button { Text = "Beobachten lassen", Disabled = true };
        FmTheme.ApplySecondaryButton(_beobachtenButton);
        _beobachtenButton.Pressed += async () =>
        {
            if (_gewaehlt != null) await TransferAktionen.BeobachtenLassen(this, _gewaehlt.SpielerId, _gewaehlt.Name, Melde);
        };
        zeile.AddChild(_beobachtenButton);

        _bietenButton = new Button { Text = "Angebot abgeben", Disabled = true };
        FmTheme.ApplyButton(_bietenButton, FmTheme.Accent);
        _bietenButton.Pressed += () => { if (_gewaehlt != null) BietenDialog.Zeige(this, _gewaehlt, Melde); };
        zeile.AddChild(_bietenButton);

        return panel;
    }

    // ── Laden ────────────────────────────────────────────────────────────────

    private async Task Lade()
    {
        var fenster = await ApiClient.GetAsync<TransferfensterModel>("transfer/fenster");
        if (!Lebt) return;
        _fensterOffen = fenster?.Offen ?? false;

        _fensterLabel.Text = fenster?.Hinweis ?? "";
        _fensterLabel.AddThemeColorOverride("font_color",
            _fensterOffen ? FmTheme.Success : FmTheme.TextSecondary);

        await Suche();
    }

    private async Task Suche()
    {
        long vereinId = GameState.Instance.VereinId;
        string position = _positionFeld.Selected <= 0 ? "" : Positionen[_positionFeld.Selected];
        int maxAlter = (int)_maxAlterFeld.Value;
        _statusLabel.Text = "Lade Transferliste …";

        var treffer = await ApiClient.GetAsync<List<TransferSpieler>>(
            $"transfer/liste?vereinId={vereinId}&position={position}&maxAlter={maxAlter}"
            + $"&nurBezahlbar={(_nurBezahlbar.ButtonPressed ? "true" : "false")}");
        if (!Lebt) return;

        if (treffer == null)
        {
            _statusLabel.Text = "Transferliste konnte nicht geladen werden.";
            return;
        }

        _gewaehlt = null;
        AktualisiereKnoepfe();
        _grid.Zeige(treffer);
        _statusLabel.Text = treffer.Count == 0
            ? "Niemand passt - Filter lockern, oder über das Scouting Spieler finden, die nicht zum Verkauf stehen."
            : $"{treffer.Count} Spieler auf der Transferliste. Doppelklick: Scoutbericht  ·  Rechtsklick: Angebot und mehr.";
    }

    private void AktualisiereKnoepfe()
    {
        _bietenButton.Disabled = _gewaehlt == null || !_fensterOffen;
        _beobachtenButton.Disabled = _gewaehlt == null;
        _bietenButton.Text = _gewaehlt == null
            ? "Angebot abgeben"
            : $"Angebot: {_gewaehlt.Name} ({FmTheme.Geld(_gewaehlt.Vorschlag)})";
    }

    private void Melde(string text)
    {
        if (Lebt) _statusLabel.Text = text;
    }
}
