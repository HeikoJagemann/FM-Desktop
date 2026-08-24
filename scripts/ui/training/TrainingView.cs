#nullable enable
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

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

    private static readonly string[] Spalten =
        { "Name", "Pos", "Alter", "Stärke", "Potenzial", "Sterne", "Frische", "Fokus",
          "Letzte Woche" };

    private OptionButton[] _schwerpunkte = new OptionButton[3];
    private OptionButton   _intensitaet = null!;
    private Label          _hinweisLabel = null!;
    private Label          _statusLabel = null!;
    private Tree           _kaderTree = null!;

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
        _kaderTree = BaueKaderTree();
        vbox.AddChild(_kaderTree);
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

    private Tree BaueKaderTree()
    {
        var tree = new Tree
        {
            Columns             = Spalten.Length,
            ColumnTitlesVisible = true,
            HideRoot            = true,
            SelectMode          = Tree.SelectModeEnum.Row,
            AllowRmbSelect      = true,
            SizeFlagsVertical   = SizeFlags.ExpandFill,
        };

        int[] breiten  = { 150, 46, 52, 76, 80, 84, 66, 130, 260 };
        bool[] expand  = { true, false, false, false, false, false, false, false, true };

        for (int i = 0; i < Spalten.Length; i++)
        {
            tree.SetColumnTitle(i, Spalten[i]);
            tree.SetColumnCustomMinimumWidth(i, breiten[i]);
            tree.SetColumnExpand(i, expand[i]);
        }
        tree.AddThemeColorOverride("title_button_color", FmTheme.TextSecondary);
        tree.ItemMouseSelected += (_, taste) => OnKaderRechtsklick(taste);
        return tree;
    }

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

    private void FuelleKader()
    {
        _kaderTree.Clear();
        var root = _kaderTree.CreateItem();

        var sortiert = _kader
            .OrderBy(s => s.Sortierung.Item1)
            .ThenBy(s => s.Sortierung.Item2)
            .ThenBy(s => s.Name)
            .ToList();

        Positionsgruppe? letzte = null;
        bool abgesetzt = false;

        foreach (var s in sortiert)
        {
            if (letzte != s.Gruppe) { abgesetzt = false; letzte = s.Gruppe; }

            _letzteWoche.TryGetValue(s.Id, out var woche);

            var item = _kaderTree.CreateItem(root);
            item.SetMetadata(0, s.Id);
            item.SetText(0, s.Name);
            item.SetText(1, s.HauptPosition);
            item.SetText(2, s.Alter.ToString());
            item.SetText(3, StaerkeText(s, woche));
            item.SetText(4, s.PotenzialText);
            item.SetText(5, s.PotenzialSterneText);
            item.SetText(6, $"{s.Frische} %");
            item.SetText(7, FokusAnzeige(s.IndividualFokus));
            item.SetText(8, WochenText(woche));

            // Mouseover auf jeder Spalte: Welche Fähigkeiten für diesen Spieler zählen. Godot
            // zeigt den Tooltip je Zelle, nicht je Zeile - deshalb überall derselbe Text.
            string hinweis = $"{s.Name} ({s.HauptPosition})\n{s.AttributrollenText}";
            for (int spalte = 0; spalte < Spalten.Length; spalte++)
                item.SetTooltipText(spalte, hinweis);

            var farbe = FmTheme.FuerGruppe(s.Gruppe, abgesetzt);
            for (int spalte = 0; spalte < Spalten.Length; spalte++)
                item.SetCustomBgColor(spalte, farbe);

            item.SetCustomColor(1, FmTheme.TextFuerGruppe(s.Gruppe));
            item.SetCustomColor(3, VeraenderungsFarbe(woche?.StaerkeDifferenz ?? 0));
            item.SetCustomColor(5, s.PotenzialSterne >= 4 ? FmTheme.Gold : FmTheme.TextSecondary);
            item.SetCustomColor(6, s.Frische >= 85 ? FmTheme.Success
                                 : s.Frische >= 70 ? FmTheme.TextPrimary
                                                   : FmTheme.Danger);
            item.SetCustomColor(7, s.IndividualFokus == null ? FmTheme.TextSecondary : FmTheme.Accent);
            item.SetCustomColor(8, WochenFarbe(woche));

            abgesetzt = !abgesetzt;
        }
    }

    /// <summary>Gesamtstärke, bei Veränderung mit Pfeil und Betrag: "48 ▲1".</summary>
    private static string StaerkeText(Spieler s, SpielerEntwicklung? woche)
    {
        int differenz = woche?.StaerkeDifferenz ?? 0;
        if (differenz == 0) return s.BestPositionStaerke.ToString();
        return $"{s.BestPositionStaerke} {(differenz > 0 ? "▲" : "▼")}{System.Math.Abs(differenz)}";
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

    private void OnKaderRechtsklick(long maustaste)
    {
        if (maustaste != (long)MouseButton.Right || _plan == null) return;

        var item = _kaderTree.GetSelected();
        if (item == null) return;
        long id = item.GetMetadata(0).AsInt64();
        var spieler = _kader.FirstOrDefault(s => s.Id == id);
        if (spieler == null) return;

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
