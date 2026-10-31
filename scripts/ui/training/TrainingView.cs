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

    // Langfristige Planung: vier Wochen mit je Intensität und drei Schwerpunkten.
    private const int ZyklusWochen = 4;
    private readonly OptionButton[,] _zyklusFelder = new OptionButton[ZyklusWochen, 4];
    private readonly Label[] _zyklusKoepfe = new Label[ZyklusWochen];
    private CheckButton _zyklusAktiv = null!;
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

        vbox.AddChild(FmTheme.MakeTitel("Training"));

        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_statusLabel);

        vbox.AddChild(BaueSteuerung());
        vbox.AddChild(BaueZyklus());

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
    /// Die langfristige Planung: vier Wochen nebeneinander, die laufende hervorgehoben. Ist sie
    /// eingeschaltet, setzt sie den Wochenplan darüber nach jeder Trainingswoche selbst - die Felder
    /// dort sind dann nur noch Anzeige.
    /// </summary>
    private Control BaueZyklus()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());
        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 10, 10);
        panel.AddChild(rand);
        var innen = new VBoxContainer();
        innen.AddThemeConstantOverride("separation", 8);
        rand.AddChild(innen);

        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 16);
        var titel = FmTheme.MakeAbschnitt("Langfristige Planung", 18);
        titel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(titel);
        _zyklusAktiv = new CheckButton { Text = "Plan verwenden" };
        _zyklusAktiv.TooltipText = "Der Wochenplan wechselt dann jede Woche von selbst zur nächsten Woche des Plans.";
        _zyklusAktiv.Toggled += async _ => await ZyklusGeaendert();
        kopf.AddChild(_zyklusAktiv);
        var ausgewogen = new Button { Text = "Ausgewogenen Plan erstellen" };
        FmTheme.ApplySecondaryButton(ausgewogen);
        ausgewogen.TooltipText = "Jeder Bereich kommt regelmäßig dran, und auf eine harte Woche folgt eine leichtere.";
        ausgewogen.Pressed += async () => await ErstelleAusgewogen();
        kopf.AddChild(ausgewogen);
        innen.AddChild(kopf);

        var wochen = new HBoxContainer();
        wochen.AddThemeConstantOverride("separation", 12);
        innen.AddChild(wochen);
        for (int w = 0; w < ZyklusWochen; w++)
        {
            var spalte = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            spalte.AddThemeConstantOverride("separation", 4);
            _zyklusKoepfe[w] = FmTheme.MakeLabel($"Woche {w + 1}", 12, FmTheme.TextSecondary);
            spalte.AddChild(_zyklusKoepfe[w]);
            for (int f = 0; f < 4; f++)
            {
                var feld = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                feld.ItemSelected += async _ => await ZyklusGeaendert();
                _zyklusFelder[w, f] = feld;
                spalte.AddChild(feld);
            }
            wochen.AddChild(spalte);
        }
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
        FuelleZyklus(_plan);
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

    /// <summary>Füllt die vier Wochen; die laufende ist hervorgehoben.</summary>
    private void FuelleZyklus(TrainingsplanModel plan)
    {
        _fuellt = true;
        _zyklusAktiv.SetPressedNoSignal(plan.ZyklusAktiv);
        for (int w = 0; w < ZyklusWochen; w++)
        {
            var woche = w < plan.Zyklus.Count ? plan.Zyklus[w] : new ZyklusWoche();
            bool laufend = plan.Zyklus.Count > 0 && w == plan.Zykluswoche;
            _zyklusKoepfe[w].Text = laufend ? $"Woche {w + 1} · diese Woche" : $"Woche {w + 1}";
            _zyklusKoepfe[w].AddThemeColorOverride("font_color", laufend ? FmTheme.Accent : FmTheme.TextSecondary);

            var intensitaet = _zyklusFelder[w, 0];
            intensitaet.Clear();
            for (int i = 0; i < plan.Intensitaeten.Count; i++)
            {
                intensitaet.AddItem(plan.Intensitaeten[i].Anzeige, i);
                intensitaet.SetItemMetadata(i, plan.Intensitaeten[i].Schluessel);
                if (plan.Intensitaeten[i].Schluessel == (woche.Intensitaet ?? "MITTEL")) intensitaet.Select(i);
            }
            string?[] gesetzt = { woche.Schwerpunkt1, woche.Schwerpunkt2, woche.Schwerpunkt3 };
            for (int f = 1; f < 4; f++)
            {
                var feld = _zyklusFelder[w, f];
                feld.Clear();
                feld.AddItem("– kein Schwerpunkt –", 0);
                feld.SetItemMetadata(0, "");
                for (int b = 0; b < plan.Bereiche.Count; b++)
                {
                    feld.AddItem(plan.Bereiche[b].Anzeige, b + 1);
                    feld.SetItemMetadata(b + 1, plan.Bereiche[b].Schluessel);
                    if (plan.Bereiche[b].Schluessel == gesetzt[f - 1]) feld.Select(b + 1);
                }
            }
        }
        // Gilt der Plan, steuert er die Woche - die Felder darüber sind dann nur Anzeige.
        foreach (var feld in _schwerpunkte) feld.Disabled = plan.ZyklusAktiv;
        _intensitaet.Disabled = plan.ZyklusAktiv;
        _fuellt = false;
    }

    private async Task ZyklusGeaendert()
    {
        if (_fuellt || _plan == null) return;
        var wochen = new List<ZyklusWoche>();
        for (int w = 0; w < ZyklusWochen; w++)
        {
            wochen.Add(new ZyklusWoche
            {
                Intensitaet  = _zyklusFelder[w, 0].GetSelectedMetadata().AsString(),
                Schwerpunkt1 = LeerAlsNull(_zyklusFelder[w, 1].GetSelectedMetadata().AsString()),
                Schwerpunkt2 = LeerAlsNull(_zyklusFelder[w, 2].GetSelectedMetadata().AsString()),
                Schwerpunkt3 = LeerAlsNull(_zyklusFelder[w, 3].GetSelectedMetadata().AsString()),
            });
        }
        var eingabe = new { aktiv = _zyklusAktiv.ButtonPressed, wochen };
        var gespeichert = await ApiClient.PutAsync<object, TrainingsplanModel>(
            $"training/{GameState.Instance.VereinId}/zyklus", eingabe);
        UebernimmPlan(gespeichert, _zyklusAktiv.ButtonPressed
            ? "Langfristige Planung gilt - der Wochenplan wechselt jetzt jede Woche von selbst."
            : "Langfristige Planung gespeichert, aber nicht in Gebrauch.");
    }

    private async Task ErstelleAusgewogen()
    {
        var gespeichert = await ApiClient.PostAsync<object, TrainingsplanModel>(
            $"training/{GameState.Instance.VereinId}/zyklus/ausgewogen", new { });
        UebernimmPlan(gespeichert, "Ausgewogener Plan erstellt und in Gebrauch: Jeder Bereich kommt in vier Wochen dran, "
                                 + "auf eine harte Woche folgt eine leichtere.");
    }

    private void UebernimmPlan(TrainingsplanModel? plan, string meldung)
    {
        if (!IsInstanceValid(this)) return;
        if (plan == null)
        {
            _statusLabel.Text = "Die langfristige Planung konnte nicht gespeichert werden.";
            return;
        }
        _plan = plan;
        FuelleAuswahlfelder(plan);
        FuelleZyklus(plan);
        _statusLabel.Text = meldung;
    }

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
        menu.AddItem(spieler.Name, -1);
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
