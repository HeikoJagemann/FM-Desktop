#nullable enable
using Godot;
using System;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Taktik;

/// <summary>
/// Wie die Mannschaft spielt: Grundausrichtung, Deckung, Gegenpressing, Passrichtung und
/// Passlänge.
///
/// <para>Jede Einstellung ist ein Segmentschalter - es sind nur zwei bis sechs Möglichkeiten,
/// und die gewählte steht sichtbar neben den anderen. Darunter steht, was die Wahl in der Partie
/// bewirkt und was sie kostet: Keine Einstellung ist immer die beste. Jede Änderung gilt sofort,
/// auch für die nächste Partie.</para>
/// </summary>
public partial class TaktikView : Control
{
    private TaktikModel _taktik = new();
    private Label _statusLabel = null!;
    private VBoxContainer _karten = null!;

    private bool Lebt => IsInstanceValid(this) && !IsQueuedForDeletion();

    /// <summary>
    /// Eine Einstellung: Werte, Beschriftung und Wirkung je Wahl. <paramref name="WirktNoch"/> ist
    /// falsch, solange die Engine sie nur speichert - Grundausrichtung und Passlänge warten auf
    /// Steilpässe, hohe Bälle und Konter, ohne die Offensive immer gewänne.
    /// </summary>
    internal sealed record Einstellung(string Titel, string Erklaerung, string[] Werte, string[] Texte,
        string[] Wirkung, Func<TaktikModel, string> Lesen, Action<TaktikModel, string> Setzen, float Knopfbreite,
        bool WirktNoch = true);

    /// <summary>Auch der Umstell-Dialog im 2D-Spiel zeigt diese Einstellungen.</summary>
    internal static readonly Einstellung[] Einstellungen =
    {
        new("Grundausrichtung",
            "Wie hoch die Mannschaft steht und wie viel sie wagt - im Ballbesitz wie gegen den Ball.",
            new[] { "BETON", "DEFENSIV", "NEUTRAL", "KONTROLLIERT_OFFENSIV", "OFFENSIV", "BRECHSTANGE" },
            new[] { "Beton anrühren", "Defensiv", "Neutral", "Kontr. Offensive", "Offensiv", "Brechstange" },
            new[]
            {
                "Alle stehen tief vor dem eigenen Tor, Risiko wird gemieden. Kaum Gegentore - aber auch kaum eigene Chancen.",
                "Die Abwehr steht tiefer, Ballverluste werden vermieden. Weniger Gegentore, weniger Chancen.",
                "Ausgewogen - so spielen die meisten Mannschaften.",
                "Etwas höher und mutiger nach vorn, ohne die Absicherung aufzugeben.",
                "Hoher Block, riskantere Pässe, früher Abschluss. Mehr Chancen - und mehr Räume für den Gegner.",
                "Alles nach vorn: Die Abwehr rückt auf, es wird aus jeder Lage geschossen. Für die letzten Minuten.",
            },
            t => t.Ausrichtung, (t, v) => t.Ausrichtung = v, 150, WirktNoch: false),
        new("Deckung",
            "Wie gegen den Ball verteidigt wird.",
            new[] { "MANN", "RAUM" },
            new[] { "Manndeckung", "Raumdeckung" },
            new[]
            {
                "Jeder Gegenspieler hat seinen Bewacher, über das ganze Feld. Eng am Mann - aber wer ihm folgt, verlässt seinen Raum und läuft mehr.",
                "Jeder hält seinen Raum, direkt gedeckt wird erst im eigenen Strafraum. Die Ordnung bleibt, die Gegner haben aber mehr Zeit am Ball.",
            },
            t => t.Deckung, (t, v) => t.Deckung = v, 160),
        new("Gegenpressing",
            "Was nach einem Ballverlust passiert.",
            new[] { "AUS", "AN" },
            new[] { "Aus", "An" },
            new[]
            {
                "Nach einem Ballverlust ordnet sich die Mannschaft zuerst und zieht sich in ihren Block zurück.",
                "Nach einem Ballverlust gehen die vier Nächsten sofort auf den Ball, und der Block rückt nicht zurück. Holt den Ball oft schnell zurück - kostet Kraft und öffnet Räume, wenn der Gegner sich befreit.",
            },
            t => t.Gegenpressing ? "AN" : "AUS", (t, v) => t.Gegenpressing = v == "AN", 110),
        new("Passrichtung",
            "Wohin der Ball im Aufbau bevorzugt gespielt wird.",
            new[] { "MITTE", "AUSGEWOGEN", "FLUEGEL" },
            new[] { "Durch die Mitte", "Ausgewogen", "Über die Flügel" },
            new[]
            {
                "Kurzer Weg zum Tor durch das Zentrum - dort ist es aber auch am engsten.",
                "Die Mannschaft spielt dorthin, wo gerade Platz ist.",
                "Breit über die Außenbahnen - mehr Platz, aber ein weiterer Weg zum Tor.",
            },
            t => t.Passrichtung, (t, v) => t.Passrichtung = v, 160),
        new("Passlänge",
            "Wie weit der Ball gespielt wird.",
            new[] { "KURZ", "GEMISCHT", "VERTIKAL" },
            new[] { "Kurzpassspiel", "Gemischt", "Vertikal nach vorn" },
            new[]
            {
                "Der nächste freie Mann: sichere Pässe und viel Ballbesitz, aber langsam nach vorn.",
                "Kurz oder lang, je nach Lage.",
                "Raumgewinn zählt: Der Ball geht schnell und gern weit nach vorn - schneller am Tor, häufiger verloren.",
            },
            t => t.Passlaenge, (t, v) => t.Passlaenge = v, 170, WirktNoch: false),
    };

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

        seite.AddChild(FmTheme.MakeTitel("Taktik"));
        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        seite.AddChild(_statusLabel);

        _karten = new VBoxContainer();
        _karten.AddThemeConstantOverride("separation", 12);
        seite.AddChild(_karten);
    }

    private async Task Lade()
    {
        var taktik = await ApiClient.GetAsync<TaktikModel>($"taktik/{GameState.Instance.VereinId}");
        if (!Lebt) return;
        if (taktik == null)
        {
            _statusLabel.Text = "Die Taktik konnte nicht geladen werden.";
            return;
        }
        _taktik = taktik;
        _statusLabel.Text = "Gilt ab sofort - auch für die nächste Partie.";
        foreach (var e in Einstellungen)
            _karten.AddChild(Karte(e));
    }

    private Control Karte(Einstellung e)
    {
        var karte = new PanelContainer();
        karte.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());
        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 18, 14);
        karte.AddChild(rand);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        rand.AddChild(box);

        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 16);
        var titel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titel.AddThemeConstantOverride("separation", 2);
        titel.AddChild(FmTheme.MakeAbschnitt(e.Titel, 20));
        titel.AddChild(FmTheme.MakeLabel(e.Erklaerung, 12, FmTheme.TextSecondary));
        kopf.AddChild(titel);
        if (!e.WirktNoch)
        {
            var hinweis = FmTheme.MakeLabel("WIRKT NOCH NICHT", 10, FmTheme.Gold);
            hinweis.AddThemeFontOverride("font", FmTheme.SchriftUi(700));
            hinweis.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            hinweis.TooltipText = "Wird gespeichert, wirkt in der Partie aber erst nach der nächsten "
                                + "Kalibrierung der Spielengine. Ohne Steilpässe, hohe Bälle und Konter "
                                + "würde eine offensive Einstellung dort immer gewinnen.";
            hinweis.MouseFilter = MouseFilterEnum.Pass;
            kopf.AddChild(hinweis);
        }
        box.AddChild(kopf);

        int auswahl = Math.Max(0, Array.IndexOf(e.Werte, e.Lesen(_taktik)));
        var schalter = Segmentschalter.Neu(e.Texte, auswahl, new Vector2(e.Knopfbreite, 36), 14);
        box.AddChild(schalter);

        var wirkung = FmTheme.MakeLabel(e.Wirkung[auswahl], 13, FmTheme.TextPrimary);
        wirkung.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(wirkung);

        schalter.Gewaehlt += i =>
        {
            e.Setzen(_taktik, e.Werte[i]);
            wirkung.Text = e.Wirkung[i];
            _ = Speichere(e.Titel, e.Texte[i]);
        };
        return karte;
    }

    private async Task Speichere(string einstellung, string wahl)
    {
        var antwort = await ApiClient.PostAsync<TaktikModel, TaktikModel>(
            $"taktik/{GameState.Instance.VereinId}", _taktik);
        if (!Lebt) return;
        _statusLabel.Text = antwort != null
            ? $"{einstellung}: {wahl} - gilt ab sofort."
            : "Die Taktik konnte nicht gespeichert werden.";
    }
}
