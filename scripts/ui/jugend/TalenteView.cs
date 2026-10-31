#nullable enable
using Godot;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Jugend;

/// <summary>
/// Was die Jugendscouts vorschlagen: einladen oder ablehnen.
///
/// <para>Ein eingeladenes Talent entscheidet sich zum Ende der Woche - je nach Ruf des Vereins und
/// seiner Jugendarbeit. Wer nicht eingeladen wird, ist nach einigen Wochen woanders. Das Talent
/// steht als Sterne da, so wie es der Scout einschätzt - ein guter Scout liegt näher an der
/// Wahrheit.</para>
/// </summary>
public partial class TalenteView : Control
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private Label _statusLabel = null!;
    private HBoxContainer _tafeln = null!;
    private VBoxContainer _offen = null!;
    private VBoxContainer _entschieden = null!;

    private bool Lebt => IsInstanceValid(this) && !IsQueuedForDeletion();

    public override async void _Ready()
    {
        BuildUI();
        await Lade();
    }

    private void BuildUI()
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(scroll);

        var seite = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        seite.AddThemeConstantOverride("separation", 14);
        scroll.AddChild(seite);

        seite.AddChild(FmTheme.MakeTitel("Talente"));
        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        seite.AddChild(_statusLabel);

        _tafeln = new HBoxContainer();
        _tafeln.AddThemeConstantOverride("separation", 12);
        seite.AddChild(_tafeln);

        seite.AddChild(FmTheme.MakeAbschnitt("Vorgeschlagen", 20));
        _offen = new VBoxContainer();
        _offen.AddThemeConstantOverride("separation", 8);
        seite.AddChild(_offen);

        seite.AddChild(FmTheme.MakeAbschnitt("Entschieden", 20));
        _entschieden = new VBoxContainer();
        _entschieden.AddThemeConstantOverride("separation", 6);
        seite.AddChild(_entschieden);
    }

    private async Task Lade()
    {
        var nachwuchs = await ApiClient.GetAsync<NachwuchsModel>($"nachwuchs/{GameState.Instance.VereinId}");
        if (!Lebt) return;
        if (nachwuchs == null)
        {
            _statusLabel.Text = "Die Talente konnten nicht geladen werden.";
            return;
        }
        if (_statusLabel.Text == "Lade …")
            _statusLabel.Text = nachwuchs.Scouts == 0
                ? "Noch kein Jugendscout beschäftigt - einstellen unter Mannschaft › Mitarbeiter. Er sichtet "
                  + "jede Woche Spieler aus der Region, überwiegend für die C-Jugend."
                : "Eingeladene Talente entscheiden sich zum Ende der Woche. Ob eines kommt, hängt am Ruf des "
                  + "Vereins und vor allem am Ruf der Jugendarbeit - und daran, wie viel es sich zutraut.";
        Zeige(nachwuchs);
    }

    private void Zeige(NachwuchsModel n)
    {
        Leere(_tafeln);
        _tafeln.AddChild(Tafel("Jugendscouts", $"{n.Scouts}", "Wer sichtet - einstellen unter Mannschaft › Mitarbeiter."));
        _tafeln.AddChild(Tafel("Vereinsruf", $"{n.Vereinsruf}", "0 bis 100 - Liga und Stärke im Vergleich zur Liga."));
        _tafeln.AddChild(Tafel("Ruf der Jugendarbeit", $"{n.Jugendruf}",
            "0 bis 100 - folgt dem Jugendgelände (Umfeld). Der wichtigste Grund, warum ein Talent kommt."));

        Leere(_offen);
        var offen = n.Talente.Where(t => t.Status is "GEFUNDEN" or "EINGELADEN").ToList();
        if (offen.Count == 0)
            _offen.AddChild(FmTheme.MakeLabel("Gerade liegt kein Vorschlag vor.", 13, FmTheme.TextSecondary));
        foreach (var t in offen) _offen.AddChild(Zeile(t));

        Leere(_entschieden);
        var entschieden = n.Talente.Where(t => t.Status is not ("GEFUNDEN" or "EINGELADEN")).ToList();
        if (entschieden.Count == 0)
            _entschieden.AddChild(FmTheme.MakeLabel("Noch nichts entschieden.", 13, FmTheme.TextSecondary));
        foreach (var t in entschieden) _entschieden.AddChild(Zeile(t));
    }

    private Control Zeile(Nachwuchstalent t)
    {
        var panel = new PanelContainer();
        var stil = FmTheme.Flaeche(FmTheme.BgPanel, 10, FmTheme.Border);
        stil.ContentMarginLeft = stil.ContentMarginRight = 16;
        stil.ContentMarginTop = stil.ContentMarginBottom = 10;
        panel.AddThemeStyleboxOverride("panel", stil);

        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 18);
        panel.AddChild(zeile);

        var name = new VBoxContainer { CustomMinimumSize = new Vector2(240, 0) };
        name.AddThemeConstantOverride("separation", 0);
        name.AddChild(FmTheme.MakeLabel(t.Name, 15, FmTheme.TextPrimary));
        name.AddChild(FmTheme.MakeLabel($"{t.Alter} Jahre · {t.KaderText} · empfohlen von {t.Scout}", 12, FmTheme.TextSecondary));
        zeile.AddChild(name);

        zeile.AddChild(Spalte("Position", t.Position, 70));
        zeile.AddChild(Spalte("Stärke", $"{t.Staerke}", 70));
        var sterne = Spieler.Sterne(t.TalentSchaetzung);
        var talent = Spalte("Talent", Spieler.SterneText(sterne), 110, FmTheme.Gold);
        talent.TooltipText = $"So schätzt ihn {t.Scout} ein: {Spieler.SterneWort(sterne)}. Ein besserer Scout liegt näher an der Wahrheit.";
        zeile.AddChild(talent);

        bool offen = t.Status is "GEFUNDEN" or "EINGELADEN";
        if (offen)
        {
            var aussichtFarbe = t.Aussicht switch { "gut" => FmTheme.Success, "gering" => FmTheme.Danger, _ => FmTheme.TextPrimary };
            var aussicht = Spalte("Aussicht", t.Aussicht, 80, aussichtFarbe);
            aussicht.TooltipText = "Wie wahrscheinlich er zusagt - nach Vereinsruf, Ruf der Jugendarbeit und seinem Talent.";
            zeile.AddChild(aussicht);
        }

        var status = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        status.AddThemeConstantOverride("separation", 0);
        status.AddChild(FmTheme.MakeLabel(t.StatusText, 13, StatusFarbe(t.Status)));
        string wann = t.Status switch
        {
            "GEFUNDEN"   => $"Ohne Einladung bis {Datum(t.VerfaelltAm)} woanders",
            "EINGELADEN" => "Entscheidet sich zum Ende der Woche",
            _            => t.Entschieden.HasValue ? $"am {Datum(t.Entschieden.Value)}" : "",
        };
        status.AddChild(FmTheme.MakeLabel(wann, 12, FmTheme.TextSecondary));
        zeile.AddChild(status);

        if (t.Status == "GEFUNDEN")
        {
            var einladen = new Button { Text = "Einladen", CustomMinimumSize = new Vector2(110, 34) };
            FmTheme.ApplyPrimaryButton(einladen);
            einladen.TooltipText = "Zum Probetraining - zum Ende der Woche sagt er zu oder ab.";
            einladen.Pressed += async () => await Entscheide(t, "einladen", $"{t.Name} ist zum Probetraining eingeladen.");
            zeile.AddChild(einladen);

            var ablehnen = new Button { Text = "Ablehnen", CustomMinimumSize = new Vector2(100, 34) };
            FmTheme.ApplySecondaryButton(ablehnen);
            ablehnen.Pressed += async () => await Entscheide(t, "verwerfen", $"{t.Name} abgelehnt.");
            zeile.AddChild(ablehnen);
        }
        return panel;
    }

    private async Task Entscheide(Nachwuchstalent t, string aktion, string meldung)
    {
        var antwort = await ApiClient.SendeAsync("POST", $"nachwuchs/{GameState.Instance.VereinId}/{t.Id}/{aktion}");
        if (!Lebt) return;
        _statusLabel.Text = antwort.Ok ? meldung : antwort.Fehler ?? "Das hat nicht geklappt.";
        await Lade();
    }

    private static Color StatusFarbe(string status) => status switch
    {
        "ZUGESAGT"   => FmTheme.Success,
        "ABGESAGT"   => FmTheme.Danger,
        "EINGELADEN" => FmTheme.Accent,
        "GEFUNDEN"   => FmTheme.TextPrimary,
        _            => FmTheme.TextSecondary,
    };

    private static Control Spalte(string titel, string wert, float breite, Color? farbe = null)
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(breite, 0) };
        box.AddThemeConstantOverride("separation", 0);
        box.AddChild(FmTheme.MakeLabel(titel.ToUpperInvariant(), 10, FmTheme.TextSecondary));
        box.AddChild(FmTheme.MakeLabel(wert, 15, farbe ?? FmTheme.TextPrimary));
        return box;
    }

    private static Control Tafel(string titel, string wert, string tooltip)
    {
        var tafel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = tooltip };
        var stil = FmTheme.Flaeche(FmTheme.BgDark, 10, FmTheme.Border);
        stil.ContentMarginLeft = stil.ContentMarginRight = 16;
        stil.ContentMarginTop = stil.ContentMarginBottom = 10;
        tafel.AddThemeStyleboxOverride("panel", stil);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 0);
        box.AddChild(FmTheme.MakeLabel(titel.ToUpperInvariant(), 10, FmTheme.TextSecondary));
        box.AddChild(FmTheme.MakeZahl(wert, 30, FmTheme.TextPrimary));
        tafel.AddChild(box);
        return tafel;
    }

    private static string Datum(DateOnly d) => d.ToString("dd.MM.yyyy", Deutsch);

    private static void Leere(Node box)
    {
        foreach (var kind in box.GetChildren()) kind.QueueFree();
    }
}
