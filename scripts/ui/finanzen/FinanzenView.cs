#nullable enable
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Finanzen;

/// <summary>
/// Finanzüberblick des Vereins: Kontostand, Wochenbilanz und Gehaltsetat auf einen Blick, der
/// Ticketpreis als einzige Stellschraube des Managers, darunter die Buchungshistorie.
/// </summary>
public partial class FinanzenView : Control
{
    private Label _statusLabel = null!;
    private Label _kontostandLabel = null!;
    private Label _wochenbilanzLabel = null!;
    private Label _gehaltsetatLabel = null!;
    private SpinBox _ticketpreisFeld = null!;
    private Label _ticketpreisHinweis = null!;
    private Button _uebernehmenButton = null!;
    private FmGrid<BuchungModel> _buchungenGrid = null!;

    private StadionModel? _stadion;
    /// <summary>Sperrt das Speichern, solange das Ticketpreis-Feld programmatisch gefüllt wird.</summary>
    private bool _fuellt;

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

        vbox.AddChild(FmTheme.MakeLabel("💶  Finanzen", 20, FmTheme.TextPrimary));

        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_statusLabel);

        var kennzahlen = new HBoxContainer();
        kennzahlen.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(kennzahlen);

        _kontostandLabel   = BaueKennzahl(kennzahlen, "Kontostand");
        _wochenbilanzLabel = BaueKennzahl(kennzahlen, "Wochenbilanz");
        _gehaltsetatLabel  = BaueKennzahl(kennzahlen, "Gehaltsetat / Woche");

        vbox.AddChild(BaueTicketpreisPanel());

        vbox.AddChild(FmTheme.MakeLabel("Buchungen", 15, FmTheme.TextPrimary));
        _buchungenGrid = new FmGrid<BuchungModel>(FinanzSpalten.Buchungsliste)
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        vbox.AddChild(_buchungenGrid);
    }

    private static Label BaueKennzahl(Container eltern, string titel)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 16);
        panel.AddChild(margin);

        var innen = new VBoxContainer();
        innen.AddThemeConstantOverride("separation", 4);
        margin.AddChild(innen);

        innen.AddChild(FmTheme.MakeLabel(titel, 12, FmTheme.TextSecondary));
        var wert = FmTheme.MakeLabel("–", 20, FmTheme.TextPrimary);
        innen.AddChild(wert);

        eltern.AddChild(panel);
        return wert;
    }

    private Control BaueTicketpreisPanel()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 16);
        panel.AddChild(margin);

        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 12);
        margin.AddChild(zeile);

        zeile.AddChild(FmTheme.MakeLabel("Ticketpreis:", 13, FmTheme.TextSecondary));

        _ticketpreisFeld = new SpinBox
        {
            MinValue = 0, MaxValue = 500, Step = 1, CustomMinimumSize = new Vector2(90, 0),
            TooltipText = "Höhere Preise bringen mehr pro Zuschauer, senken aber die Auslastung.",
        };
        _ticketpreisFeld.ValueChanged += _ => { if (!_fuellt) _uebernehmenButton.Disabled = false; };
        zeile.AddChild(_ticketpreisFeld);

        _uebernehmenButton = new Button { Text = "Übernehmen", Disabled = true };
        FmTheme.ApplyButton(_uebernehmenButton, FmTheme.Accent);
        _uebernehmenButton.Pressed += async () => await SpeichereTicketpreis();
        zeile.AddChild(_uebernehmenButton);

        _ticketpreisHinweis = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        zeile.AddChild(_ticketpreisHinweis);

        return panel;
    }

    // ── Laden ────────────────────────────────────────────────────────────────

    private async Task Lade()
    {
        long vereinId = GameState.Instance.VereinId;

        var finanzenTask = ApiClient.GetAsync<FinanzenModel>($"finanzen/{vereinId}");
        var stadionTask  = ApiClient.GetAsync<StadionModel>($"verein/{vereinId}/stadion");

        var finanzen = await finanzenTask;
        _stadion = await stadionTask;

        if (finanzen == null)
        {
            _statusLabel.Text = "Finanzdaten konnten nicht geladen werden.";
            return;
        }

        _kontostandLabel.Text   = FmTheme.Geld(finanzen.Kontostand);
        _kontostandLabel.AddThemeColorOverride("font_color",
            finanzen.Kontostand >= 0 ? FmTheme.Success : FmTheme.Danger);

        _wochenbilanzLabel.Text = (finanzen.Wochenbilanz >= 0 ? "+" : "") + FmTheme.Geld(finanzen.Wochenbilanz);
        _wochenbilanzLabel.AddThemeColorOverride("font_color",
            finanzen.Wochenbilanz >= 0 ? FmTheme.Success : FmTheme.Danger);

        _gehaltsetatLabel.Text = FmTheme.Geld(finanzen.Gehaltsetat);

        _fuellt = true;
        _ticketpreisFeld.Value = _stadion?.Ticketpreis ?? finanzen.Ticketpreis;
        _fuellt = false;
        _uebernehmenButton.Disabled = true;
        _ticketpreisHinweis.Text = _stadion != null
            ? $"{_stadion.Name} · Kapazität {_stadion.Kapazitaet:N0}"
            : "Kein Stadion gefunden.";
        _ticketpreisFeld.Editable = _stadion != null;
        _uebernehmenButton.Visible = _stadion != null;

        _buchungenGrid.Zeige(finanzen.Buchungen);

        _statusLabel.Text = finanzen.Buchungen.Count == 0
            ? "Noch keine Buchungen - der erste Wochendurchlauf bringt Gehälter und Betriebskosten."
            : $"{finanzen.Buchungen.Count} Buchungen";
    }

    private async Task SpeichereTicketpreis()
    {
        if (_stadion == null) return;

        _stadion.Ticketpreis = (int)_ticketpreisFeld.Value;
        var ergebnis = await ApiClient.PutAsync<StadionModel, StadionModel>(
            $"stadion/{_stadion.Id}", _stadion);

        if (ergebnis == null)
        {
            _statusLabel.Text = "Ticketpreis konnte nicht gespeichert werden.";
            return;
        }

        _stadion = ergebnis;
        _uebernehmenButton.Disabled = true;
        _statusLabel.Text = $"Ticketpreis auf {ergebnis.Ticketpreis} € gesetzt.";
    }
}
