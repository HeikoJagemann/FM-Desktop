#nullable enable
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Training;

/// <summary>
/// Trainingssteuerung: drei Mannschaftsschwerpunkte je Woche, eine Intensität und ein
/// persönlicher Schwerpunkt je Spieler.
///
/// <para>Der Plan wird sofort beim Ändern gespeichert - genau wie die Aufstellung. Ein
/// Speichern-Button würde nur die Frage aufwerfen, mit welchem Stand gespielt wird.</para>
/// </summary>
public partial class TrainingView : Control
{
    private const string KeinFokus = "–";

    private OptionButton[]  _schwerpunkte = new OptionButton[3];
    private OptionButton    _intensitaet  = null!;
    private Label           _hinweisLabel = null!;
    private Label           _statusLabel  = null!;
    private FmGrid<Spieler> _kaderGrid    = null!;

    private TrainingsplanModel? _plan;
    private List<Spieler> _kader = new();
    /// Entwicklung der letzten Trainingswoche je Spieler-ID.
    private Dictionary<long, SpielerEntwicklung> _letzteWoche = new();
    /// Sperrt das Speichern, solange die Auswahlfelder programmatisch gefüllt werden.
    private bool _fuellt;

    public override async void _Ready()
    {
        BuildUI();
        await Lade();
    }

    // ── Aufbau ───────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 10);
        AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel("💪  Training", 20, FmTheme.TextPrimary));

        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_statusLabel);

        vbox.AddChild(BaueSteuerung());

        vbox.AddChild(FmTheme.MakeLabel("Kader", 15, FmTheme.TextPrimary));
        _kaderGrid = BaueKaderGrid();
        vbox.AddChild(_kaderGrid);
    }

    private Control BaueSteuerung()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());

        var innen = new VBoxContainer();
        innen.AddThemeConstantOverride("separation", 6);
        var rand = new MarginContainer();
        foreach (var seite in new[] { "left", "right", "top", "bottom" })
            rand.AddThemeConstantOverride($"margin_{seite}", 10);
        rand.AddChild(innen);
        panel.AddChild(rand);

        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 12);
        innen.AddChild(zeile);

        for (int i = 0; i < _schwerpunkte.Length; i++)
        {
            var spalte = new VBoxContainer();
            spalte.AddChild(FmTheme.MakeLabel($"Schwerpunkt {i + 1}", 12, FmTheme.TextSecondary));

            var auswahl = new OptionButton { CustomMinimumSize = new Vector2(190, 0) };
            int index = i;
            auswahl.ItemSelected += async _ => await PlanGeaendert();
            auswahl.MouseEntered += () => ZeigeHinweis(index);
            spalte.AddChild(auswahl);

            _schwerpunkte[i] = auswahl;
            zeile.AddChild(spalte);
        }

        var intensitaetSpalte = new VBoxContainer();
        intensitaetSpalte.AddChild(FmTheme.MakeLabel("Intensität", 12, FmTheme.TextSecondary));
        _intensitaet = new OptionButton { CustomMinimumSize = new Vector2(140, 0) };
        _intensitaet.ItemSelected += async _ => await PlanGeaendert();
        intensitaetSpalte.AddChild(_intensitaet);
        zeile.AddChild(intensitaetSpalte);

        _hinweisLabel = FmTheme.MakeLabel(
            "Hohe Intensität bringt mehr Fortschritt, kostet aber Frische für das nächste Spiel.",
            12, FmTheme.TextSecondary);
        innen.AddChild(_hinweisLabel);

        return panel;
    }

    /// <summary>
    /// Spalten aus dem gemeinsamen Katalog. Die Stärke erbt dadurch denselben Mouseover wie in
    /// allen anderen Ansichten - vorher wurde er hier von einer Schleife über alle Spalten
    /// überschrieben und war wirkungslos.
    /// </summary>
    private FmGrid<Spieler> BaueKaderGrid()
    {
        var fokus = new GridSpalte<Spieler>
        {
            Titel = "Fokus", Breite = 130,
            Text    = s => FokusAnzeige(s.IndividualFokus),
            Farbe   = s => s.IndividualFokus == null ? FmTheme.TextSecondary : FmTheme.Accent,
            Tooltip = _ => "Persönlicher Schwerpunkt - per Rechtsklick zu setzen.",
        };

        var letzteWoche = new GridSpalte<Spieler>
        {
            Titel = "Letzte Woche", Breite = 260, Expand = true,
            Text  = s => WochenText(WocheVon(s)),
            Farbe = s => WochenFarbe(WocheVon(s)),
        };

        var grid = new FmGrid<Spieler>(
            SpielerSpalten.Trainingsliste(s => WocheVon(s)?.StaerkeDifferenz ?? 0, fokus, letzteWoche))
        {
            SizeFlagsVertical  = SizeFlags.ExpandFill,
            Zeilenfarbe        = SpielerSpalten.Zeilenfarbe,
            ZebraNeuBei        = s => s.Gruppe,
            Standardsortierung = SpielerSpalten.NachAufstellung,
        };
        grid.Rechtsklick += OnKaderRechtsklick;
        return grid;
    }

    private SpielerEntwicklung? WocheVon(Spieler s) =>
        _letzteWoche.TryGetValue(s.Id, out var woche) ? woche : null;

    // ── Laden ────────────────────────────────────────────────────────────────

    private async Task Lade()
    {
        long vereinId = GameState.Instance.VereinId;

        _plan = await ApiClient.GetAsync<TrainingsplanModel>($"training/{vereinId}");
        var spieler = await ApiClient.GetAsync<List<Spieler>>($"verein/{vereinId}/spieler");
        var bericht = await ApiClient.GetAsync<List<SpielerEntwicklung>>($"training/{vereinId}/bericht");

        if (_plan == null || spieler == null)
        {
            _statusLabel.Text = "Trainingsdaten konnten nicht geladen werden.";
            return;
        }

        _kader = spieler.Where(s => s.Kader is "Profi" or "Amateur").ToList();
        _letzteWoche = (bericht ?? new List<SpielerEntwicklung>())
            .GroupBy(e => e.SpielerId)
            .ToDictionary(g => g.Key, g => g.First());

        FuelleAuswahlfelder(_plan);
        FuelleKader();

        int veraendert = _letzteWoche.Count(e => e.Value.Aenderungen.Count > 0);
        _statusLabel.Text = $"{_kader.Count} Spieler im Training, {veraendert} haben sich letzte "
                          + "Woche verändert. Rechtsklick setzt den persönlichen Schwerpunkt.";
    }

    private void FuelleAuswahlfelder(TrainingsplanModel plan)
    {
        _fuellt = true;

        string?[] gesetzt = { plan.Schwerpunkt1, plan.Schwerpunkt2, plan.Schwerpunkt3 };
        for (int i = 0; i < _schwerpunkte.Length; i++)
        {
            var auswahl = _schwerpunkte[i];
            auswahl.Clear();
            auswahl.AddItem("– kein Schwerpunkt –", 0);
            auswahl.SetItemMetadata(0, "");

            for (int b = 0; b < plan.Bereiche.Count; b++)
            {
                var bereich = plan.Bereiche[b];
                auswahl.AddItem(bereich.Anzeige, b + 1);
                auswahl.SetItemMetadata(b + 1, bereich.Schluessel);
                if (bereich.Schluessel == gesetzt[i]) auswahl.Select(b + 1);
            }
        }

        _intensitaet.Clear();
        for (int i = 0; i < plan.Intensitaeten.Count; i++)
        {
            var stufe = plan.Intensitaeten[i];
            _intensitaet.AddItem(stufe.Anzeige, i);
            _intensitaet.SetItemMetadata(i, stufe.Schluessel);
            if (stufe.Schluessel == plan.Intensitaet) _intensitaet.Select(i);
        }

        _fuellt = false;
    }

    private void FuelleKader() => _kaderGrid.Zeige(_kader);

    /// <summary>Die veränderten Fähigkeiten der letzten Woche, stärkste Veränderung zuerst.</summary>
    private static string WochenText(SpielerEntwicklung? woche)
    {
        if (woche == null || woche.Aenderungen.Count == 0) return "";
        return string.Join("  ", woche.Aenderungen.Select(a => a.Kurz));
    }

    /// <summary>Grün bei überwiegendem Fortschritt, rot bei überwiegendem Abbau.</summary>
    private static Color WochenFarbe(SpielerEntwicklung? woche)
    {
        if (woche == null || woche.Aenderungen.Count == 0) return FmTheme.TextSecondary;
        int summe = woche.Aenderungen.Sum(a => a.Differenz);
        return VeraenderungsFarbe(summe);
    }

    private static Color VeraenderungsFarbe(int differenz) => differenz switch
    {
        > 0 => FmTheme.Success,
        < 0 => FmTheme.Danger,
        _   => FmTheme.TextPrimary,
    };

    private string FokusAnzeige(string? schluessel)
    {
        if (string.IsNullOrEmpty(schluessel)) return KeinFokus;
        var bereich = _plan?.Bereiche.FirstOrDefault(b => b.Schluessel == schluessel);
        return bereich?.Anzeige ?? schluessel;
    }

    // ── Reaktionen ───────────────────────────────────────────────────────────

    private void ZeigeHinweis(int index)
    {
        var schluessel = _schwerpunkte[index].GetSelectedMetadata().AsString();
        var bereich = _plan?.Bereiche.FirstOrDefault(b => b.Schluessel == schluessel);
        _hinweisLabel.Text = bereich == null
            ? "Ohne Schwerpunkt trainiert die Mannschaft nur die Grundlagen."
            : $"{bereich.Anzeige} schult: {bereich.Beschreibung}";
    }

    private async Task PlanGeaendert()
    {
        if (_fuellt || _plan == null) return;

        var eingabe = new TrainingsplanEingabe
        {
            Intensitaet  = _intensitaet.GetSelectedMetadata().AsString(),
            Schwerpunkt1 = LeerAlsNull(_schwerpunkte[0].GetSelectedMetadata().AsString()),
            Schwerpunkt2 = LeerAlsNull(_schwerpunkte[1].GetSelectedMetadata().AsString()),
            Schwerpunkt3 = LeerAlsNull(_schwerpunkte[2].GetSelectedMetadata().AsString()),
        };

        long vereinId = GameState.Instance.VereinId;
        var gespeichert = await ApiClient.PutAsync<TrainingsplanEingabe, TrainingsplanModel>(
            $"training/{vereinId}", eingabe);

        if (gespeichert == null)
        {
            _statusLabel.Text = "Trainingsplan konnte nicht gespeichert werden.";
            return;
        }
        _plan = gespeichert;
        _statusLabel.Text = "Trainingsplan gespeichert.";
    }

    private static string? LeerAlsNull(string wert) => string.IsNullOrEmpty(wert) ? null : wert;

    private void OnKaderRechtsklick(Spieler spieler)
    {
        if (_plan == null) return;

        var menu = new PopupMenu();
        menu.AddItem($"👤  {spieler.Name}", -1);
        menu.SetItemDisabled(0, true);
        menu.AddSeparator("Persönlicher Schwerpunkt");
        menu.AddItem("– keiner –", 0);

        for (int i = 0; i < _plan.Bereiche.Count; i++)
            menu.AddItem(_plan.Bereiche[i].Anzeige, i + 1);

        AddChild(menu);
        menu.IdPressed += async id2 =>
        {
            if (id2 < 0) return;
            string? fokus = id2 == 0 ? null : _plan.Bereiche[(int)id2 - 1].Schluessel;
            await SetzeFokus(spieler, fokus);
        };
        menu.PopupHide += menu.QueueFree;
        menu.Position = (Vector2I)GetGlobalMousePosition();
        menu.Popup();
    }

    private async Task SetzeFokus(Spieler spieler, string? fokus)
    {
        await ApiClient.PutAsync<object, object>(
            $"training/spieler/{spieler.Id}/fokus", new { fokus = fokus ?? "" });

        spieler.IndividualFokus = fokus;
        FuelleKader();
        _statusLabel.Text = fokus == null
            ? $"Persönlicher Schwerpunkt für {spieler.Name} entfernt."
            : $"{spieler.Name} trainiert jetzt zusätzlich {FokusAnzeige(fokus)}.";
    }
}
