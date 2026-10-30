#nullable enable
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Transfer;

/// <summary>
/// Die Scoutabteilung: Scouts einstellen und beauftragen, und was sie berichten.
///
/// <para>Fremde Spieler kennt der Verein nur ungefähr. Jede Beobachtung macht Stärke und Talent
/// genauer - wie genau, hängt von der Qualität des Scouts ab. Aus den Berichten heraus wird
/// abgeworben, auch wer nicht auf der Transferliste steht.</para>
/// </summary>
public partial class ScoutingView : Control
{
    private static readonly string[] Positionen =
        { "Alle", "TW", "IV", "LV", "RV", "DM", "ZM", "LM", "RM", "OM", "RA", "LA", "HS", "ST" };

    private Label _statusLabel = null!;
    private Label _scoutsLabel = null!;
    private FmGrid<ScoutModel> _scoutGrid = null!;
    private Button _einstellenButton = null!;
    private Button _auftragButton = null!;
    private Button _beendenButton = null!;
    private Button _entlassenButton = null!;

    private OptionButton _positionFeld = null!;
    private SpinBox _maxAlterFeld = null!;
    private CheckBox _nurListe = null!;
    private FmGrid<TransferSpieler> _berichtGrid = null!;
    private Label _berichteLabel = null!;

    private ScoutingModel? _scouting;
    private ScoutModel? _scout;
    private bool _fensterOffen;

    private bool Lebt => IsInstanceValid(this) && !IsQueuedForDeletion();

    public override async void _Ready()
    {
        BuildUI();
        var fenster = await ApiClient.GetAsync<TransferfensterModel>("transfer/fenster");
        if (!Lebt) return;
        _fensterOffen = fenster?.Offen ?? false;
        await LadeScouts();
        if (!Lebt) return;
        await LadeBerichte();
    }

    private void BuildUI()
    {
        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 10);
        AddChild(vbox);

        vbox.AddChild(FmTheme.MakeTitel("Scouting"));
        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_statusLabel);

        // ── Scouts ──
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 10);
        _scoutsLabel = FmTheme.MakeLabel("Scouts", 15, FmTheme.TextPrimary);
        _scoutsLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(_scoutsLabel);

        _einstellenButton = Knopf("Scout einstellen …", () => ZeigeBewerber());
        _auftragButton = Knopf("Auftrag …", () => { if (_scout != null) AuftragDialog.Zeige(this, _scout, NachAenderung); });
        _beendenButton = Knopf("Auftrag beenden", async () => await BeendeAuftrag());
        _entlassenButton = Knopf("Entlassen …", () => FrageEntlassen());
        kopf.AddChild(_einstellenButton);
        kopf.AddChild(_auftragButton);
        kopf.AddChild(_beendenButton);
        kopf.AddChild(_entlassenButton);
        vbox.AddChild(kopf);

        _scoutGrid = new FmGrid<ScoutModel>(ScoutSpalten) { CustomMinimumSize = new Vector2(0, 150) };
        _scoutGrid.Ausgewaehlt += s => { _scout = s; AktualisiereKnoepfe(); };
        _scoutGrid.Doppelklick += s => AuftragDialog.Zeige(this, s, NachAenderung);
        vbox.AddChild(_scoutGrid);

        // ── Berichte ──
        _berichteLabel = FmTheme.MakeLabel("Berichte", 15, FmTheme.TextPrimary);
        vbox.AddChild(_berichteLabel);
        vbox.AddChild(BaueFilterleiste());

        _berichtGrid = new FmGrid<TransferSpieler>(TransferSpalten.Berichte)
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _berichtGrid.Doppelklick += s => ScoutberichtDialog.Zeige(this, s, _fensterOffen, Melde);
        _berichtGrid.Rechtsklick += s => TransferAktionen.Zeige(this, s, _fensterOffen, Melde);
        vbox.AddChild(_berichtGrid);

        AktualisiereKnoepfe();
    }

    private static Button Knopf(string text, System.Action gedrueckt)
    {
        var knopf = new Button { Text = text };
        FmTheme.ApplySecondaryButton(knopf);
        knopf.Pressed += gedrueckt;
        return knopf;
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
        _positionFeld.ItemSelected += async _ => await LadeBerichte();
        zeile.AddChild(_positionFeld);

        zeile.AddChild(FmTheme.MakeLabel("Höchstalter:", 13, FmTheme.TextSecondary));
        _maxAlterFeld = new SpinBox
        {
            MinValue = 0, MaxValue = 40, Step = 1, Value = 0,
            CustomMinimumSize = new Vector2(80, 0), TooltipText = "0 = jedes Alter",
        };
        zeile.AddChild(_maxAlterFeld);

        _nurListe = new CheckBox { Text = "nur Transferliste" };
        _nurListe.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
        zeile.AddChild(_nurListe);

        var suchen = new Button { Text = "🔍  Filtern" };
        FmTheme.ApplyButton(suchen, FmTheme.BgPanel);
        suchen.AddThemeColorOverride("font_color", FmTheme.TextPrimary);
        suchen.Pressed += async () => await LadeBerichte();
        zeile.AddChild(suchen);

        zeile.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        zeile.AddChild(FmTheme.MakeLabel("Doppelklick: Scoutbericht  ·  Rechtsklick: Angebot und mehr", 12, FmTheme.TextGedimmt));
        return panel;
    }

    // ── Laden ────────────────────────────────────────────────────────────────

    private async Task LadeScouts()
    {
        var scouting = await ApiClient.GetAsync<ScoutingModel>($"scouting/{GameState.Instance.VereinId}");
        if (!Lebt) return;
        if (scouting == null)
        {
            _statusLabel.Text = "Die Scoutabteilung konnte nicht geladen werden.";
            return;
        }
        _scouting = scouting;
        _scout = null;
        _scoutGrid.Zeige(scouting.Scouts);
        _scoutsLabel.Text = $"Scouts ({scouting.Scouts.Count}/{scouting.MaxScouts})";
        _statusLabel.Text = scouting.Scouts.Count == 0
            ? "Noch kein Scout beschäftigt. Ohne Scouts kennst du fremde Spieler nur grob - und ihr Talent gar nicht."
            : "Scouts beobachten jede Woche. Doppelklick auf einen Scout ändert seinen Auftrag.";
        AktualisiereKnoepfe();
    }

    private async Task LadeBerichte()
    {
        string position = _positionFeld.Selected <= 0 ? "" : Positionen[_positionFeld.Selected];
        var berichte = await ApiClient.GetAsync<List<TransferSpieler>>(
            $"scouting/berichte?vereinId={GameState.Instance.VereinId}&position={position}"
            + $"&maxAlter={(int)_maxAlterFeld.Value}&nurTransferliste={(_nurListe.ButtonPressed ? "true" : "false")}");
        if (!Lebt) return;

        var liste = berichte ?? new List<TransferSpieler>();
        _berichtGrid.Zeige(liste);
        _berichteLabel.Text = liste.Count == 0
            ? "Berichte - noch keine. Auch die Gegner der eigenen Partien landen hier."
            : $"Berichte ({liste.Count} Spieler)";
    }

    private void AktualisiereKnoepfe()
    {
        bool gewaehlt = _scout != null;
        _auftragButton.Disabled = !gewaehlt;
        _beendenButton.Disabled = !gewaehlt || string.IsNullOrEmpty(_scout?.Auftrag);
        _entlassenButton.Disabled = !gewaehlt;
        _einstellenButton.Disabled = _scouting != null && _scouting.Scouts.Count >= _scouting.MaxScouts;
    }

    // ── Aktionen ─────────────────────────────────────────────────────────────

    private void ZeigeBewerber()
    {
        if (_scouting == null) return;
        var menu = new PopupMenu();
        menu.AddItem("Bewerber in diesem Monat (Wochengehalt)", -1);
        menu.SetItemDisabled(0, true);
        menu.AddSeparator();
        foreach (var b in _scouting.Bewerber)
        {
            menu.AddItem($"{b.Name}  ·  Qualität {b.Qualitaet}  ·  {FmTheme.Gehalt(b.Gehalt)}", b.Nummer);
        }
        AddChild(menu);
        menu.IdPressed += async id =>
        {
            if (id < 0) return;
            var antwort = await ApiClient.SendeAsync("POST",
                $"scouting/{GameState.Instance.VereinId}/bewerber/{id}");
            Melde(antwort.Ok ? "Scout eingestellt - jetzt einen Auftrag geben." : antwort.Fehler ?? "Abgelehnt.");
            if (Lebt && antwort.Ok) await LadeScouts();
        };
        menu.PopupHide += menu.QueueFree;
        menu.Position = (Vector2I)GetGlobalMousePosition();
        menu.Popup();
    }

    private void FrageEntlassen()
    {
        if (_scout == null || _scouting == null) return;
        var scout = _scout;
        long abfindung = (long)scout.Gehalt * _scouting.AbfindungWochen;

        var menu = new PopupMenu();
        menu.AddItem($"{scout.Name} entlassen?", -1);
        menu.SetItemDisabled(0, true);
        menu.AddSeparator();
        menu.AddItem($"✔  Ja - Abfindung {FmTheme.Geld(abfindung)}", 1);
        menu.AddItem("✖  Nein", 0);
        AddChild(menu);
        menu.IdPressed += async id =>
        {
            if (id != 1) return;
            var antwort = await ApiClient.SendeAsync("DELETE", $"scouting/scout/{scout.Id}");
            Melde(antwort.Ok ? $"{scout.Name} entlassen." : antwort.Fehler ?? "Abgelehnt.");
            if (Lebt) await LadeScouts();
        };
        menu.PopupHide += menu.QueueFree;
        menu.Position = (Vector2I)GetGlobalMousePosition();
        menu.Popup();
    }

    private async Task BeendeAuftrag()
    {
        if (_scout == null) return;
        var antwort = await ApiClient.SendeAsync("DELETE", $"scouting/scout/{_scout.Id}/auftrag");
        Melde(antwort.Ok ? $"{_scout.Name} hat keinen Auftrag mehr." : antwort.Fehler ?? "Abgelehnt.");
        if (Lebt) await LadeScouts();
    }

    private async void NachAenderung(string text)
    {
        Melde(text);
        if (Lebt) await LadeScouts();
    }

    private void Melde(string text)
    {
        if (Lebt) _statusLabel.Text = text;
    }

    // ── Spalten ──────────────────────────────────────────────────────────────

    private static IEnumerable<GridSpalte<ScoutModel>> ScoutSpalten => new[]
    {
        new GridSpalte<ScoutModel>
        {
            Titel = "Name", Breite = 160, Expand = true, ExpandGewicht = 2,
            Text = s => s.Name, Sortierung = s => s.Name,
        },
        new GridSpalte<ScoutModel>
        {
            Titel = "Qualität", Breite = 70, Ausrichtung = HorizontalAlignment.Center,
            Text = s => s.Qualitaet.ToString(),
            Farbe = s => s.Qualitaet >= 70 ? FmTheme.Success : s.Qualitaet >= 45 ? FmTheme.TextPrimary : FmTheme.TextSecondary,
            Tooltip = _ => "Je besser der Scout, desto genauer seine Einschätzung - vor allem beim Talent.",
            Sortierung = s => -s.Qualitaet,
        },
        new GridSpalte<ScoutModel>
        {
            Titel = FmTheme.GehaltTitel, Breite = 110, Ausrichtung = HorizontalAlignment.Right,
            Text = s => FmTheme.Gehalt(s.Gehalt), Sortierung = s => -s.Gehalt,
        },
        new GridSpalte<ScoutModel>
        {
            Titel = "Auftrag", Breite = 260, Expand = true, ExpandGewicht = 4,
            Text = s => string.IsNullOrEmpty(s.Auftrag) ? "ohne Auftrag" : s.Auftrag,
            Farbe = s => string.IsNullOrEmpty(s.Auftrag) ? FmTheme.Danger : FmTheme.TextPrimary,
            Sortierung = s => s.Auftrag,
        },
    };
}
