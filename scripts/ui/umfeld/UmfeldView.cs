#nullable enable
using Godot;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Umfeld;

/// <summary>
/// Das Umfeld des Vereins: Stadion, Trainingsgelände und Jugendgelände nebeneinander.
///
/// <para>Jede Karte zeigt, was die Anlage heute ist und bewirkt, und darunter den nächsten
/// Ausbau mit Kosten, Bauzeit und dem, was er danach bewirkt und kostet. Bezahlt wird beim
/// Baubeginn, wirksam ist ein Ausbau erst, wenn er fertig ist - deshalb fragt jeder Bau vorher
/// nach.</para>
/// </summary>
public partial class UmfeldView : Control
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private Label _statusLabel = null!;
    private HBoxContainer _tafeln = null!;
    private HBoxContainer _karten = null!;
    private UmfeldModel? _umfeld;

    private bool Lebt => IsInstanceValid(this) && !IsQueuedForDeletion();

    public override async void _Ready()
    {
        BuildUI();
        await Lade();
    }

    private void BuildUI()
    {
        var seite = new VBoxContainer();
        seite.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        seite.AddThemeConstantOverride("separation", 14);
        AddChild(seite);

        seite.AddChild(FmTheme.MakeTitel("Umfeld"));
        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        seite.AddChild(_statusLabel);

        _tafeln = new HBoxContainer();
        _tafeln.AddThemeConstantOverride("separation", 12);
        seite.AddChild(_tafeln);

        _karten = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _karten.AddThemeConstantOverride("separation", 12);
        seite.AddChild(_karten);
    }

    private async Task Lade()
    {
        var umfeld = await ApiClient.GetAsync<UmfeldModel>($"umfeld/{GameState.Instance.VereinId}");
        if (!Lebt) return;
        if (umfeld == null)
        {
            _statusLabel.Text = "Das Umfeld konnte nicht geladen werden.";
            return;
        }
        _umfeld = umfeld;
        if (_statusLabel.Text == "Lade …")
            _statusLabel.Text = "Ein Ausbau wird beim Baubeginn bezahlt und wirkt, sobald er fertig ist. "
                              + "Was über dem Standard der Liga liegt, kostet zusätzlich Unterhalt.";
        Zeige(umfeld);
    }

    private void Zeige(UmfeldModel u)
    {
        Leere(_tafeln);
        _tafeln.AddChild(Tafel("Kontostand", FmTheme.Geld(u.Kontostand),
            u.Kontostand >= 0 ? FmTheme.TextPrimary : FmTheme.Danger, "Daraus wird jeder Ausbau bezahlt."));
        _tafeln.AddChild(Tafel("Vereinsruf", $"{u.Vereinsruf}", FmTheme.TextPrimary,
            "0 bis 100 - Liga und Stärke im Vergleich zur Liga. Zählt, wenn ein Talent sich für einen Verein entscheidet."));
        _tafeln.AddChild(Tafel("Ruf der Jugendarbeit", $"{u.Jugendruf}", FmTheme.TextPrimary,
            "0 bis 100 - folgt dem Jugendgelände. Der wichtigste Grund, warum ein Talent zum Verein kommt."));
        _tafeln.AddChild(Tafel("Im Bau", u.Projekte.Count == 0 ? "–" : $"{u.Projekte.Count}", FmTheme.TextPrimary,
            u.Projekte.Count == 0 ? "Gerade wird nichts gebaut."
                : string.Join("\n", u.Projekte.Select(p => $"{p.Anzeige}: fertig am {Datum(p.Fertig)}"))));

        Leere(_karten);
        if (u.Stadion != null) _karten.AddChild(StadionKarte(u.Stadion, u));
        if (u.Trainingsgelaende != null)
            _karten.AddChild(GelaendeKarte(u.Trainingsgelaende, u, "Trainingsfortschritt der Herren"));
        if (u.Jugendgelaende != null)
            _karten.AddChild(GelaendeKarte(u.Jugendgelaende, u, "Entwicklung der Jugend"));
    }

    // ── Stadion ──────────────────────────────────────────────────────────────

    private Control StadionKarte(StadionUmfeld s, UmfeldModel u)
    {
        var (karte, box) = Karte("Stadion", s.Name);

        box.AddChild(Kennzahl("Plätze", s.Kapazitaet.ToString("N0", Deutsch)));
        box.AddChild(Zeile("Eintritt", $"{s.Ticketpreis} €"));
        box.AddChild(Zeile("Unterhalt je Woche", FmTheme.Geld(s.UnterhaltWoche),
            "Steckt in den Betriebskosten und wächst mit jedem Platz."));
        box.AddChild(Trenner());

        var projekt = u.Projekte.FirstOrDefault(p => p.Art == "STADION");
        if (projekt != null)
        {
            box.AddChild(FmTheme.MakeAbschnitt("Im Bau", 16));
            box.AddChild(Hinweis($"Ausbau auf {projekt.Ziel.ToString("N0", Deutsch)} Plätze - fertig am {Datum(projekt.Fertig)}. "
                                 + "Bis dahin gilt die alte Größe."));
            return karte;
        }
        if (s.Ausbau.Count == 0)
        {
            box.AddChild(Hinweis("Größer wird kein Stadion im Land."));
            return karte;
        }

        box.AddChild(FmTheme.MakeAbschnitt("Ausbauen", 16));
        foreach (var a in s.Ausbau)
        {
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 10);
            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            text.AddThemeConstantOverride("separation", 0);
            text.AddChild(FmTheme.MakeLabel($"+{a.Plaetze.ToString("N0", Deutsch)} auf {a.NeueKapazitaet.ToString("N0", Deutsch)}",
                14, FmTheme.TextPrimary));
            text.AddChild(FmTheme.MakeLabel($"{FmTheme.Geld(a.Kosten)} · {a.Wochen} Wochen", 12, FmTheme.TextSecondary));
            zeile.AddChild(text);
            var knopf = Knopf("Bauen", a.Kosten <= u.Kontostand);
            knopf.TooltipText = a.Kosten <= u.Kontostand ? "" : "Dafür reicht das Geld nicht.";
            var ausbau = a;
            knopf.Pressed += () => FrageBau($"Stadion auf {ausbau.NeueKapazitaet.ToString("N0", Deutsch)} Plätze ausbauen?",
                ausbau.Kosten, "STADION", ausbau.NeueKapazitaet);
            zeile.AddChild(knopf);
            box.AddChild(zeile);
        }
        return karte;
    }

    // ── Gelände ──────────────────────────────────────────────────────────────

    private Control GelaendeKarte(Gelaende g, UmfeldModel u, string wirktAuf)
    {
        var (karte, box) = Karte(g.Titel, g.Name);

        box.AddChild(Stufen(g.Stufe, g.MaxStufe, g.Standard));
        box.AddChild(Zeile("Wirkung", Prozent(g.Faktor), $"Auf die {wirktAuf}, gemessen an Stufe 3."));
        box.AddChild(Zeile("Ligastandard", $"Stufe {g.Standard}",
            "Den bringt jeder Verein der Liga mit; sein Unterhalt steckt in den Betriebskosten."));
        box.AddChild(Zeile("Mehrkosten je Woche", g.MehrkostenWoche > 0 ? FmTheme.Geld(g.MehrkostenWoche) : "keine",
            "Unterhalt über den Ligastandard hinaus."));
        box.AddChild(Trenner());

        var projekt = u.Projekte.FirstOrDefault(p => p.Art == g.Art);
        if (projekt != null)
        {
            box.AddChild(FmTheme.MakeAbschnitt("Im Bau", 16));
            box.AddChild(Hinweis($"Stufe {projekt.Ziel} - fertig am {Datum(projekt.Fertig)}."));
            return karte;
        }
        if (g.Naechste == null)
        {
            box.AddChild(Hinweis("Höchste Stufe erreicht."));
            return karte;
        }

        var n = g.Naechste;
        box.AddChild(FmTheme.MakeAbschnitt($"Nächste Stufe: {n.Name}", 16));
        box.AddChild(Zeile("Kosten", FmTheme.Geld(n.Kosten)));
        box.AddChild(Zeile("Bauzeit", $"{n.Wochen} Wochen"));
        box.AddChild(Zeile("Wirkung danach", Prozent(n.Faktor)));
        box.AddChild(Zeile("Mehrkosten danach", n.MehrkostenWoche > 0 ? $"{FmTheme.Geld(n.MehrkostenWoche)} je Woche" : "keine"));
        var knopf = Knopf("Ausbauen", n.Kosten <= u.Kontostand);
        knopf.TooltipText = n.Kosten <= u.Kontostand ? "" : "Dafür reicht das Geld nicht.";
        knopf.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        knopf.Pressed += () => FrageBau($"{g.Titel} auf Stufe {n.Stufe} ausbauen ({n.Name})?", n.Kosten, g.Art, n.Stufe);
        box.AddChild(knopf);
        return karte;
    }

    /// <summary>Fünf Felder, die erreichten gefüllt; der Ligastandard ist umrandet.</summary>
    private static Control Stufen(int stufe, int max, int standard)
    {
        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", 6);
        for (int i = 1; i <= max; i++)
        {
            var feld = new Panel { CustomMinimumSize = new Vector2(34, 12) };
            var farbe = i <= stufe ? FmTheme.Accent : FmTheme.BgKnopf;
            feld.AddThemeStyleboxOverride("panel",
                FmTheme.Flaeche(farbe, 4, i == standard ? FmTheme.TextSecondary : FmTheme.Border, i == standard ? 2 : 1));
            feld.TooltipText = i == standard ? $"Stufe {i} - Standard der Liga" : $"Stufe {i}";
            reihe.AddChild(feld);
        }
        var text = FmTheme.MakeLabel($"Stufe {stufe} von {max}", 12, FmTheme.TextSecondary);
        text.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        reihe.AddChild(text);
        return reihe;
    }

    private static string Prozent(double faktor)
    {
        double p = (faktor - 1.0) * 100.0;
        return Math.Abs(p) < 0.5 ? "±0 %" : $"{(p > 0 ? "+" : "−")}{Math.Abs(p).ToString("0", Deutsch)} %";
    }

    // ── Bauen ────────────────────────────────────────────────────────────────

    private void FrageBau(string frage, long kosten, string art, int ziel)
    {
        var menu = new PopupMenu();
        menu.AddItem(frage, -1);
        menu.SetItemDisabled(0, true);
        menu.AddSeparator();
        menu.AddItem($"Ja - {FmTheme.Geld(kosten)} bezahlen und bauen", 1);
        menu.AddItem("Nein", 0);
        AddChild(menu);
        menu.IdPressed += async id =>
        {
            if (id != 1) return;
            var antwort = await ApiClient.SendeAsync("POST", $"umfeld/{GameState.Instance.VereinId}/bau",
                new { art, ziel });
            if (!Lebt) return;
            _statusLabel.Text = antwort.Ok ? "Der Bau hat begonnen." : antwort.Fehler ?? "Der Bau wurde abgelehnt.";
            await Lade();
        };
        menu.PopupHide += menu.QueueFree;
        menu.Position = (Vector2I)GetGlobalMousePosition();
        menu.Popup();
    }

    // ── Bausteine ────────────────────────────────────────────────────────────

    private static (Control Karte, VBoxContainer Box) Karte(string titel, string untertitel)
    {
        var karte = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        karte.SizeFlagsStretchRatio = 1;
        karte.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());
        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 18, 14);
        karte.AddChild(rand);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        rand.AddChild(box);
        box.AddChild(FmTheme.MakeAbschnitt(titel, 22));
        box.AddChild(FmTheme.MakeLabel(untertitel, 13, FmTheme.TextSecondary));
        return (karte, box);
    }

    private static Control Kennzahl(string titel, string wert)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 0);
        box.AddChild(FmTheme.MakeLabel(titel.ToUpperInvariant(), 10, FmTheme.TextSecondary));
        box.AddChild(FmTheme.MakeZahl(wert, 34, FmTheme.TextPrimary));
        return box;
    }

    private static Control Zeile(string beschriftung, string wert, string tooltip = "")
    {
        var zeile = new HBoxContainer { TooltipText = tooltip };
        zeile.AddThemeConstantOverride("separation", 8);
        var b = FmTheme.MakeLabel(beschriftung, 13, FmTheme.TextSecondary);
        b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        b.MouseFilter = MouseFilterEnum.Pass;
        zeile.AddChild(b);
        var w = FmTheme.MakeLabel(wert, 13, FmTheme.TextPrimary);
        w.MouseFilter = MouseFilterEnum.Pass;
        zeile.AddChild(w);
        return zeile;
    }

    private static Label Hinweis(string text)
    {
        var label = FmTheme.MakeLabel(text, 13, FmTheme.TextSecondary);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(200, 0);
        return label;
    }

    private static Control Trenner()
    {
        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        return sep;
    }

    private static Button Knopf(string text, bool moeglich)
    {
        var knopf = new Button { Text = text, CustomMinimumSize = new Vector2(110, 32), Disabled = !moeglich };
        FmTheme.ApplyPrimaryButton(knopf);
        return knopf;
    }

    private static Control Tafel(string titel, string wert, Color farbe, string tooltip)
    {
        var tafel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = tooltip };
        var stil = FmTheme.Flaeche(FmTheme.BgDark, 10, FmTheme.Border);
        stil.ContentMarginLeft = stil.ContentMarginRight = 16;
        stil.ContentMarginTop = stil.ContentMarginBottom = 10;
        tafel.AddThemeStyleboxOverride("panel", stil);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 0);
        box.AddChild(FmTheme.MakeLabel(titel.ToUpperInvariant(), 10, FmTheme.TextSecondary));
        box.AddChild(FmTheme.MakeZahl(wert, 30, farbe));
        tafel.AddChild(box);
        return tafel;
    }

    private static string Datum(DateOnly d) => d.ToString("dd.MM.yyyy", Deutsch);

    private static void Leere(Node box)
    {
        foreach (var kind in box.GetChildren()) kind.QueueFree();
    }
}
