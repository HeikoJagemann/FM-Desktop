#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Kalender;

/// <summary>
/// Der Saisonkalender: links das Monatsraster, rechts die nächsten Termine.
///
/// <para>Der Spielplan ist damit kein eigener Zeitstrahl mehr, sondern liegt im Kalender - samt
/// Vorbereitung, Winterpause und Saisonende.</para>
/// </summary>
public partial class KalenderView : Control
{
    private static readonly string[] Wochentage = { "Mo", "Di", "Mi", "Do", "Fr", "Sa", "So" };

    private Label _monatLabel = null!;
    private Label _statusLabel = null!;
    private GridContainer _raster = null!;
    private VBoxContainer _terminliste = null!;

    private KalenderStand? _stand;
    private DateOnly _angezeigterMonat;
    private readonly Dictionary<DateOnly, List<Termin>> _termineProTag = new();

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

        vbox.AddChild(FmTheme.MakeLabel("📅  Kalender", 20, FmTheme.TextPrimary));

        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_statusLabel);

        var spalten = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        spalten.AddThemeConstantOverride("separation", 14);
        vbox.AddChild(spalten);

        spalten.AddChild(BaueMonatsspalte());
        spalten.AddChild(BaueTerminspalte());
    }

    private Control BaueMonatsspalte()
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());

        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 12);
        panel.AddChild(rand);

        var inhalt = new VBoxContainer();
        inhalt.AddThemeConstantOverride("separation", 8);
        rand.AddChild(inhalt);

        // Kopfzeile mit Blättern
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 8);

        var zurueck = new Button { Text = "◀", CustomMinimumSize = new Vector2(38, 0) };
        FmTheme.ApplyButton(zurueck, FmTheme.BgPanel);
        zurueck.Pressed += async () => await BlaettereUm(-1);
        kopf.AddChild(zurueck);

        _monatLabel = FmTheme.MakeLabel("", 16, FmTheme.TextPrimary, HorizontalAlignment.Center);
        _monatLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(_monatLabel);

        var vor = new Button { Text = "▶", CustomMinimumSize = new Vector2(38, 0) };
        FmTheme.ApplyButton(vor, FmTheme.BgPanel);
        vor.Pressed += async () => await BlaettereUm(1);
        kopf.AddChild(vor);

        inhalt.AddChild(kopf);

        // Wochentagszeile
        var tage = new GridContainer { Columns = 7 };
        tage.AddThemeConstantOverride("h_separation", 4);
        foreach (var tag in Wochentage)
        {
            var label = FmTheme.MakeLabel(tag, 11, FmTheme.TextSecondary, HorizontalAlignment.Center);
            label.CustomMinimumSize = new Vector2(74, 0);
            tage.AddChild(label);
        }
        inhalt.AddChild(tage);

        _raster = new GridContainer { Columns = 7, SizeFlagsVertical = SizeFlags.ExpandFill };
        _raster.AddThemeConstantOverride("h_separation", 4);
        _raster.AddThemeConstantOverride("v_separation", 4);
        inhalt.AddChild(_raster);

        return panel;
    }

    private Control BaueTerminspalte()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(330, 0) };
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());

        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 12);
        panel.AddChild(rand);

        var inhalt = new VBoxContainer();
        inhalt.AddThemeConstantOverride("separation", 8);
        rand.AddChild(inhalt);

        inhalt.AddChild(FmTheme.MakeLabel("Nächste Termine", 15, FmTheme.TextPrimary));

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        inhalt.AddChild(scroll);

        _terminliste = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _terminliste.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_terminliste);

        return panel;
    }

    // ── Laden ────────────────────────────────────────────────────────────────

    private async Task Lade()
    {
        _stand = await ApiClient.GetAsync<KalenderStand>("kalender");
        if (_stand == null)
        {
            _statusLabel.Text = "Der Kalender konnte nicht geladen werden.";
            return;
        }

        GameState.Instance.SetSpieldatum(_stand.Datum);
        _angezeigterMonat = new DateOnly(_stand.Datum.Year, _stand.Datum.Month, 1);

        _statusLabel.Text = $"Saison {_stand.SaisonText} · heute ist {_stand.DatumText} · {_stand.Phase}";

        await LadeMonat();
        await LadeNaechsteTermine();
    }

    private async Task BlaettereUm(int monate)
    {
        _angezeigterMonat = _angezeigterMonat.AddMonths(monate);
        await LadeMonat();
    }

    private async Task LadeMonat()
    {
        DateOnly ende = _angezeigterMonat.AddMonths(1).AddDays(-1);
        var termine = await ApiClient.GetAsync<List<Termin>>(
            $"kalender/termine?von={_angezeigterMonat:yyyy-MM-dd}&bis={ende:yyyy-MM-dd}");

        _termineProTag.Clear();
        foreach (var termin in termine ?? new List<Termin>())
        {
            if (!_termineProTag.TryGetValue(termin.Datum, out var liste))
            {
                liste = new List<Termin>();
                _termineProTag[termin.Datum] = liste;
            }
            liste.Add(termin);
        }

        _monatLabel.Text = $"{KalenderStand.Monatsname(_angezeigterMonat.Month)} {_angezeigterMonat.Year}";
        ZeichneRaster();
    }

    /// <summary>Die kommenden Termine ab heute - unabhängig vom angezeigten Monat.</summary>
    private async Task LadeNaechsteTermine()
    {
        if (_stand == null) return;

        var termine = await ApiClient.GetAsync<List<Termin>>(
            $"kalender/termine?von={_stand.Datum:yyyy-MM-dd}&bis={_stand.Datum.AddDays(75):yyyy-MM-dd}");

        foreach (Node kind in _terminliste.GetChildren()) kind.QueueFree();

        var naechste = (termine ?? new List<Termin>())
            .OrderBy(t => t.Datum)
            .ThenBy(t => t.ZeitText)
            .Take(15)
            .ToList();

        if (naechste.Count == 0)
        {
            _terminliste.AddChild(FmTheme.MakeLabel("Keine Termine.", 12, FmTheme.TextSecondary));
            return;
        }

        foreach (var termin in naechste)
        {
            _terminliste.AddChild(BaueTerminzeile(termin));
        }
    }

    // ── Darstellung ──────────────────────────────────────────────────────────

    private void ZeichneRaster()
    {
        foreach (Node kind in _raster.GetChildren()) kind.QueueFree();

        // Montag als erster Wochentag - der Kalender beginnt links mit dem Wochenstart.
        int vorlauf = ((int)_angezeigterMonat.DayOfWeek + 6) % 7;
        for (int i = 0; i < vorlauf; i++)
        {
            _raster.AddChild(new Control { CustomMinimumSize = new Vector2(74, 62) });
        }

        int tage = DateTime.DaysInMonth(_angezeigterMonat.Year, _angezeigterMonat.Month);
        for (int tag = 1; tag <= tage; tag++)
        {
            _raster.AddChild(BaueTagesfeld(new DateOnly(_angezeigterMonat.Year, _angezeigterMonat.Month, tag)));
        }
    }

    private Control BaueTagesfeld(DateOnly datum)
    {
        _termineProTag.TryGetValue(datum, out var termine);
        bool heute = _stand != null && datum == _stand.Datum;
        var eigenes = termine?.FirstOrDefault(t => t.EigenesSpiel);

        var feld = new PanelContainer { CustomMinimumSize = new Vector2(74, 62) };
        var stil = new StyleBoxFlat
        {
            BgColor = Hintergrund(termine, eigenes),
            CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        };
        if (heute)
        {
            // Der heutige Tag bekommt einen Rahmen statt einer Füllung - so bleibt erkennbar,
            // ob an ihm auch ein Spiel liegt.
            stil.BorderColor = FmTheme.Gold;
            stil.SetBorderWidthAll(2);
        }
        feld.AddThemeStyleboxOverride("panel", stil);

        var inhalt = new VBoxContainer();
        inhalt.AddThemeConstantOverride("separation", 0);
        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 4);
        rand.AddChild(inhalt);
        feld.AddChild(rand);

        inhalt.AddChild(FmTheme.MakeLabel(datum.Day.ToString(), 12,
            heute ? FmTheme.Gold : FmTheme.TextSecondary));

        if (eigenes != null)
        {
            inhalt.AddChild(FmTheme.MakeLabel(eigenes.Ergebnis ?? eigenes.ZeitText, 11,
                FmTheme.TextPrimary));
            inhalt.AddChild(FmTheme.MakeLabel(Gegner(eigenes), 10, FmTheme.TextSecondary));
            feld.TooltipText = $"{eigenes.Titel}\n{eigenes.Untertitel}";
        }
        else if (termine != null && termine.Count > 0)
        {
            var erster = termine[0];
            inhalt.AddChild(FmTheme.MakeLabel(erster.IstSpiel ? erster.Untertitel : erster.Titel,
                10, FmTheme.TextSecondary));
            feld.TooltipText = string.Join("\n", termine.Select(t => t.Titel));
        }

        return feld;
    }

    /// <summary>Eigenes Spiel sticht hervor, Ligaspiele und Abschnitte bleiben dezent.</summary>
    private Color Hintergrund(List<Termin>? termine, Termin? eigenes)
    {
        if (eigenes != null)
        {
            return eigenes.Ergebnis != null
                ? FmTheme.BgPanel.Lerp(FmTheme.Success, 0.25f)
                : FmTheme.BgPanel.Lerp(FmTheme.Accent, 0.35f);
        }
        if (termine == null || termine.Count == 0)
        {
            return FmTheme.BgDark;
        }
        return termine.Any(t => t.IstSpiel)
            ? FmTheme.BgPanel
            : FmTheme.BgPanel.Lerp(FmTheme.Gold, 0.18f);
    }

    private Control BaueTerminzeile(Termin termin)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 8);

        var datum = FmTheme.MakeLabel($"{termin.Datum:dd.MM.}", 11, FmTheme.TextSecondary);
        datum.CustomMinimumSize = new Vector2(52, 0);
        zeile.AddChild(datum);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 0);
        text.AddChild(FmTheme.MakeLabel(termin.Titel, 12,
            termin.EigenesSpiel ? FmTheme.Accent : FmTheme.TextPrimary));

        string unten = termin.IstSpiel
            ? $"{termin.Untertitel}{(termin.ZeitText.Length > 0 ? " · " + termin.ZeitText : "")}"
            : termin.Untertitel;
        text.AddChild(FmTheme.MakeLabel(unten, 10, FmTheme.TextSecondary));
        zeile.AddChild(text);

        if (termin.Ergebnis != null)
        {
            zeile.AddChild(FmTheme.MakeLabel(termin.Ergebnis, 12, FmTheme.Success));
        }
        return zeile;
    }

    /// <summary>Kurzform für das Tagesfeld: der Gegner, nicht die ganze Paarung.</summary>
    private string Gegner(Termin termin)
    {
        var teile = termin.Titel.Split(" – ");
        if (teile.Length != 2)
        {
            return termin.Titel;
        }
        string eigener = GameState.Instance.VereinName;
        string gegner = teile[0] == eigener ? teile[1] : teile[0];
        return gegner.Length > 11 ? gegner[..11] : gegner;
    }
}
