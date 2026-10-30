#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Transfer;

/// <summary>
/// Gibt einem Scout einen Auftrag: eine Liga durchsuchen - auf Wunsch nur eine Position und bis zu
/// einem Höchstalter - oder die Spieler eines Vereins beobachten. Einen einzelnen Spieler beobachten
/// lässt man direkt aus einer Spielerliste heraus.
/// </summary>
public partial class AuftragDialog : Overlaykarte
{
    private static readonly string[] Positionen =
        { "Alle", "TW", "IV", "LV", "RV", "DM", "ZM", "LM", "RM", "OM", "RA", "LA", "HS", "ST" };

    private ScoutModel _scout = null!;
    private Action<string>? _erteilt;

    private OptionButton _art = null!;
    private OptionButton _liga = null!;
    private OptionButton _verein = null!;
    private OptionButton _position = null!;
    private SpinBox _maxAlter = null!;
    private Control _vereinZeile = null!;
    private Control _filterZeilen = null!;

    private List<Models.Liga> _ligen = new();
    private List<Verein> _vereine = new();
    private List<Verein> _vereineDerLiga = new();

    public AuftragDialog() : base("Auftrag für den Scout") { }

    public static void Zeige(Node caller, ScoutModel scout, Action<string> erteilt)
    {
        Oeffne(caller, new AuftragDialog { _scout = scout, _erteilt = erteilt });
    }

    protected override void BaueInhalt(VBoxContainer inhalt)
    {
        inhalt.AddChild(FmTheme.MakeLabel($"{_scout.Name}  ·  Qualität {_scout.Qualitaet}", 16, FmTheme.TextPrimary));

        _art = new OptionButton();
        _art.AddItem("Liga durchsuchen", 0);
        _art.AddItem("Verein beobachten", 1);
        _art.ItemSelected += _ => AktualisiereSichtbarkeit();
        inhalt.AddChild(Zeile("Auftrag", _art));

        _liga = new OptionButton();
        _liga.ItemSelected += _ => FuelleVereine();
        inhalt.AddChild(Zeile("Liga", _liga));

        _verein = new OptionButton();
        _vereinZeile = Zeile("Verein", _verein);
        inhalt.AddChild(_vereinZeile);

        var filter = new VBoxContainer();
        filter.AddThemeConstantOverride("separation", 12);
        _position = new OptionButton();
        foreach (var p in Positionen) _position.AddItem(p);
        filter.AddChild(Zeile("Position", _position));
        _maxAlter = new SpinBox { MinValue = 0, MaxValue = 40, Step = 1, Value = 0, Suffix = "Jahre" };
        _maxAlter.TooltipText = "0 = jedes Alter";
        filter.AddChild(Zeile("Höchstalter", _maxAlter));
        _filterZeilen = filter;
        inhalt.AddChild(filter);

        var hinweis = FmTheme.MakeLabel(
            "Je Woche schaut sich der Scout in einer Liga zehn, bei einem Verein acht Spieler an - "
            + "zuerst die, über die er noch am wenigsten weiß.", 12, FmTheme.TextGedimmt);
        hinweis.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hinweis.CustomMinimumSize = new Vector2(400, 0);
        inhalt.AddChild(hinweis);

        inhalt.AddChild(Knopfleiste("Auftrag erteilen", async () => await Erteile()));

        AktualisiereSichtbarkeit();
        _ = Lade();
    }

    private async System.Threading.Tasks.Task Lade()
    {
        var ligen = await ApiClient.GetAsync<List<Models.Liga>>("liga");
        var vereine = await ApiClient.GetAsync<List<Verein>>("verein");
        if (!IsInstanceValid(this)) return;

        _ligen = (ligen ?? new()).OrderBy(l => l.Stufe).ThenBy(l => l.Name).ToList();
        _vereine = vereine ?? new();
        _liga.Clear();
        foreach (var l in _ligen) _liga.AddItem(l.Name);

        // Vorauswahl: die eigene Liga - dort spielt man gegen die Vereine ohnehin.
        long eigeneLiga = GameState.Instance.LigaId;
        int index = _ligen.FindIndex(l => l.Id == eigeneLiga);
        if (index >= 0) _liga.Select(index);
        FuelleVereine();
    }

    private void FuelleVereine()
    {
        _verein.Clear();
        if (_liga.Selected < 0 || _liga.Selected >= _ligen.Count) return;
        long ligaId = _ligen[_liga.Selected].Id;
        long eigener = GameState.Instance.VereinId;
        _vereineDerLiga = _vereine
            .Where(v => v.Liga?.Id == ligaId && v.Id != eigener)
            .OrderBy(v => v.Name)
            .ToList();
        foreach (var v in _vereineDerLiga) _verein.AddItem(v.Name);
    }

    private void AktualisiereSichtbarkeit()
    {
        bool liga = _art.Selected == 0;
        _vereinZeile.Visible = !liga;
        _filterZeilen.Visible = liga;
    }

    private async System.Threading.Tasks.Task Erteile()
    {
        bool liga = _art.Selected == 0;
        object auftrag;
        if (liga)
        {
            if (_liga.Selected < 0) { ZeigeFehler("Bitte eine Liga wählen."); return; }
            auftrag = new
            {
                art = "LIGA",
                ziel = _ligen[_liga.Selected].Id,
                position = _position.Selected <= 0 ? null : Positionen[_position.Selected],
                maxAlter = _maxAlter.Value > 0 ? (int?)_maxAlter.Value : null,
            };
        }
        else
        {
            if (_verein.Selected < 0) { ZeigeFehler("Bitte einen Verein wählen."); return; }
            auftrag = new { art = "VEREIN", ziel = _vereineDerLiga[_verein.Selected].Id };
        }

        var antwort = await ApiClient.SendeAsync("PUT", $"scouting/scout/{_scout.Id}/auftrag", auftrag);
        if (!IsInstanceValid(this)) return;
        if (!antwort.Ok)
        {
            ZeigeFehler(antwort.Fehler ?? "Der Auftrag wurde abgelehnt.");
            return;
        }
        _erteilt?.Invoke($"{_scout.Name} hat einen neuen Auftrag.");
        Schliessen();
    }
}
