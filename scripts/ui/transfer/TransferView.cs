#nullable enable
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Transfer;

/// <summary>
/// Der Transfermarkt: Spieler suchen und Angebote abgeben, dazu die Angebote anderer Vereine für
/// eigene Spieler.
///
/// <para>Gewechselt wird nur im offenen Transferfenster - außerhalb bleibt die Ansicht sichtbar,
/// aber die Knöpfe sind gesperrt, damit erkennbar ist, dass es den Markt gibt und wann er wieder
/// öffnet.</para>
/// </summary>
public partial class TransferView : Control
{
    private static readonly string[] Positionen =
        { "Alle", "TW", "IV", "LV", "RV", "DM", "ZM", "LM", "RM", "OM", "RA", "LA", "HS", "ST" };

    private Label _statusLabel = null!;
    private Label _fensterLabel = null!;
    private OptionButton _positionFeld = null!;
    private SpinBox _minStaerkeFeld = null!;
    private CheckBox _nurBezahlbar = null!;
    private Button _suchenButton = null!;
    private Button _bietenButton = null!;
    private FmGrid<TransferSpieler> _suchGrid = null!;
    private FmGrid<TransferangebotModel> _angeboteGrid = null!;
    private Label _angeboteLabel = null!;

    private TransferSpieler? _gewaehlt;
    private bool _fensterOffen;

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

        vbox.AddChild(FmTheme.MakeLabel("🔁  Transfermarkt", 20, FmTheme.TextPrimary));

        _fensterLabel = FmTheme.MakeLabel("", 13, FmTheme.TextSecondary);
        vbox.AddChild(_fensterLabel);

        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_statusLabel);

        vbox.AddChild(BaueSuchleiste());

        _suchGrid = new FmGrid<TransferSpieler>(TransferSpalten.Suchergebnis)
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _suchGrid.Ausgewaehlt += s => { _gewaehlt = s; AktualisiereBietenButton(); };
        vbox.AddChild(_suchGrid);

        _angeboteLabel = FmTheme.MakeLabel("Angebote für eigene Spieler", 15, FmTheme.TextPrimary);
        vbox.AddChild(_angeboteLabel);

        _angeboteGrid = new FmGrid<TransferangebotModel>(TransferSpalten.Angebotsliste(true))
        {
            CustomMinimumSize = new Vector2(0, 160),
        };
        _angeboteGrid.Rechtsklick += ZeigeAngebotsmenue;
        vbox.AddChild(_angeboteGrid);
    }

    private Control BaueSuchleiste()
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
        zeile.AddChild(_positionFeld);

        zeile.AddChild(FmTheme.MakeLabel("Mindeststärke:", 13, FmTheme.TextSecondary));
        _minStaerkeFeld = new SpinBox
        {
            MinValue = 0, MaxValue = 100, Step = 1, Value = 0,
            CustomMinimumSize = new Vector2(80, 0),
        };
        zeile.AddChild(_minStaerkeFeld);

        _nurBezahlbar = new CheckBox { Text = "nur bezahlbare", ButtonPressed = true };
        _nurBezahlbar.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
        _nurBezahlbar.TooltipText = "Blendet Spieler aus, deren Ablöse den Kontostand übersteigt.";
        zeile.AddChild(_nurBezahlbar);

        _suchenButton = new Button { Text = "🔍  Suchen" };
        FmTheme.ApplyButton(_suchenButton, FmTheme.BgPanel);
        _suchenButton.AddThemeColorOverride("font_color", FmTheme.TextPrimary);
        _suchenButton.Pressed += async () => await Suche();
        zeile.AddChild(_suchenButton);

        zeile.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _bietenButton = new Button { Text = "Angebot abgeben", Disabled = true };
        FmTheme.ApplyButton(_bietenButton, FmTheme.Accent);
        _bietenButton.Pressed += async () => await Biete();
        zeile.AddChild(_bietenButton);

        return panel;
    }

    // ── Laden ────────────────────────────────────────────────────────────────

    private async Task Lade()
    {
        var fenster = await ApiClient.GetAsync<TransferfensterModel>("transfer/fenster");
        _fensterOffen = fenster?.Offen ?? false;

        _fensterLabel.Text = fenster?.Hinweis ?? "";
        _fensterLabel.AddThemeColorOverride("font_color",
            _fensterOffen ? FmTheme.Success : FmTheme.TextSecondary);

        await LadeAngebote();
        await Suche();
    }

    private async Task LadeAngebote()
    {
        long vereinId = GameState.Instance.VereinId;
        var angebote = await ApiClient.GetAsync<List<TransferangebotModel>>(
            $"transfer/eingehend/{vereinId}");

        _angeboteGrid.Zeige(angebote ?? new List<TransferangebotModel>());
        _angeboteLabel.Text = angebote is { Count: > 0 }
            ? $"Angebote für eigene Spieler ({angebote.Count}) - Rechtsklick zum Entscheiden"
            : "Angebote für eigene Spieler - derzeit keine";
    }

    private async Task Suche()
    {
        long vereinId = GameState.Instance.VereinId;
        string position = _positionFeld.Selected <= 0 ? "" : Positionen[_positionFeld.Selected];
        int minStaerke = (int)_minStaerkeFeld.Value;

        var treffer = await ApiClient.GetAsync<List<TransferSpieler>>(
            $"transfer/suche?vereinId={vereinId}&position={position}&minStaerke={minStaerke}");

        if (treffer == null)
        {
            _statusLabel.Text = "Transfermarkt konnte nicht geladen werden.";
            return;
        }

        if (_nurBezahlbar.ButtonPressed)
        {
            treffer = treffer.FindAll(s => s.Bezahlbar);
        }

        _gewaehlt = null;
        AktualisiereBietenButton();
        _suchGrid.Zeige(treffer);
        _statusLabel.Text = treffer.Count == 0
            ? "Keine Spieler gefunden - Filter lockern."
            : $"{treffer.Count} Spieler gefunden. Zeile anklicken, dann Angebot abgeben.";
    }

    // ── Aktionen ─────────────────────────────────────────────────────────────

    private void AktualisiereBietenButton()
    {
        _bietenButton.Disabled = _gewaehlt == null || !_fensterOffen;
        _bietenButton.Text = _gewaehlt == null
            ? "Angebot abgeben"
            : $"Angebot: {_gewaehlt.Name} ({FmTheme.Geld(_gewaehlt.Abloese)})";
    }

    private async Task Biete()
    {
        if (_gewaehlt == null) return;

        long vereinId = GameState.Instance.VereinId;
        var ergebnis = await ApiClient.PostAsync<object, TransferangebotModel>(
            $"transfer/angebot?spielerId={_gewaehlt.SpielerId}&vereinId={vereinId}", new { });

        if (ergebnis == null)
        {
            _statusLabel.Text = $"Angebot für {_gewaehlt.Name} wurde abgelehnt.";
            return;
        }

        _statusLabel.Text = $"Angebot für {ergebnis.SpielerName} über "
                          + $"{FmTheme.Geld(ergebnis.Abloese)} abgegeben.";
        await Suche();
    }

    /// <summary>Eingehende Angebote annehmen oder ablehnen.</summary>
    private void ZeigeAngebotsmenue(TransferangebotModel angebot)
    {
        var menu = new PopupMenu();
        menu.AddItem($"📄  {angebot.SpielerName} → {angebot.NachVerein}", -1);
        menu.SetItemDisabled(0, true);
        menu.AddSeparator();
        menu.AddItem($"✔  Annehmen ({FmTheme.Geld(angebot.Abloese)})", 0);
        menu.AddItem("✖  Ablehnen", 1);

        if (!_fensterOffen)
        {
            menu.SetItemDisabled(2, true);
        }

        AddChild(menu);
        menu.IdPressed += async id =>
        {
            if (id == 0) await Entscheide(angebot, "annehmen");
            else if (id == 1) await Entscheide(angebot, "ablehnen");
        };
        menu.PopupHide += menu.QueueFree;
        menu.Position = (Vector2I)GetGlobalMousePosition();
        menu.Popup();
    }

    private async Task Entscheide(TransferangebotModel angebot, string aktion)
    {
        var ergebnis = await ApiClient.PostAsync<object, TransferangebotModel>(
            $"transfer/angebot/{angebot.Id}/{aktion}", new { });

        _statusLabel.Text = ergebnis == null
            ? $"Das Angebot für {angebot.SpielerName} konnte nicht bearbeitet werden."
            : aktion == "annehmen"
                ? $"{angebot.SpielerName} wechselt für {FmTheme.Geld(angebot.Abloese)} zu {angebot.NachVerein}."
                : $"Angebot für {angebot.SpielerName} abgelehnt.";

        await LadeAngebote();
    }
}
