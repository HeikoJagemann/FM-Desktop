#nullable enable
using Godot;
using System.Globalization;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Personal;

/// <summary>
/// Der Stab des Vereins: Co-Trainer und Physiotherapeuten einstellen und entlassen.
///
/// <para>Oben steht, was der Stab gerade bewirkt - gemessen an einem Verein ohne erfassten Stab,
/// wie ihn die anderen Vereine haben. Darunter je Rolle eine Karte mit den Beschäftigten und den
/// Bewerbern des Monats. Scouts haben ihre eigene Seite unter Transfers, weil sie Aufträge
/// bekommen.</para>
/// </summary>
public partial class MitarbeiterView : Control
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private HBoxContainer _wirkung = null!;
    private HBoxContainer _abteilungen = null!;
    private Label _statusLabel = null!;

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

        seite.AddChild(FmTheme.MakeTitel("Mitarbeiter"));
        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        seite.AddChild(_statusLabel);

        _wirkung = new HBoxContainer();
        _wirkung.AddThemeConstantOverride("separation", 12);
        seite.AddChild(_wirkung);

        _abteilungen = new HBoxContainer();
        _abteilungen.AddThemeConstantOverride("separation", 12);
        seite.AddChild(_abteilungen);
    }

    private async Task Lade()
    {
        var stab = await ApiClient.GetAsync<StabModel>($"mitarbeiter/{GameState.Instance.VereinId}");
        if (!Lebt) return;
        if (stab == null)
        {
            _statusLabel.Text = "Der Stab konnte nicht geladen werden.";
            return;
        }
        if (_statusLabel.Text == "Lade …")
            _statusLabel.Text = $"Bewerber wechseln jeden Monat. Wer entlassen wird, bekommt "
                                + $"{stab.AbfindungWochen} Wochengehälter Abfindung.";
        ZeigeWirkung(stab);
        ZeigeAbteilungen(stab);
    }

    // ── Wirkung ──────────────────────────────────────────────────────────────

    private void ZeigeWirkung(StabModel stab)
    {
        Leere(_wirkung);

        double training = (stab.Trainingsfaktor - 1.0) * 100.0;
        _wirkung.AddChild(Tafel("Training",
            $"{Vorzeichen(training)}{System.Math.Abs(training).ToString("0", Deutsch)} %", Wertfarbe(training),
            "Trainingsfortschritt aller Spieler gegenüber einem Verein ohne eigenen Stab. "
            + "Ein Co-Trainer mittlerer Qualität (50) gleicht ihn aus, ohne Co-Trainer fehlen zehn Prozent."));

        double erholung = stab.Erholungsbonus;
        _wirkung.AddChild(Tafel("Erholung",
            $"{Vorzeichen(erholung)}{System.Math.Abs(erholung).ToString("0.0", Deutsch)}", Wertfarbe(erholung),
            "Frischepunkte je Woche gegenüber einem Verein ohne eigenen Stab - so viel frischer "
            + "geht jeder Spieler ins nächste Spiel. Ein Physiotherapeut mittlerer Qualität gleicht ihn aus.",
            "Frische je Woche"));

        _wirkung.AddChild(Tafel("Kosten", FmTheme.Gehalt(stab.Wochenkosten), FmTheme.TextPrimary,
            "Gehälter des Stabs; gebucht wird wöchentlich.", Gehaltsformat.Titel));
    }

    private static string Vorzeichen(double wert) => wert > 0.05 ? "+" : wert < -0.05 ? "−" : "±";

    private static Color Wertfarbe(double wert) =>
        wert > 0.05 ? FmTheme.Success : wert < -0.05 ? FmTheme.Danger : FmTheme.TextPrimary;

    private static Control Tafel(string titel, string wert, Color farbe, string tooltip, string einheit = "")
    {
        var tafel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = tooltip };
        var stil = FmTheme.Flaeche(FmTheme.BgDark, 10, FmTheme.Border);
        stil.ContentMarginLeft = stil.ContentMarginRight = 16;
        stil.ContentMarginTop = stil.ContentMarginBottom = 10;
        tafel.AddThemeStyleboxOverride("panel", stil);

        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 0);
        box.AddChild(FmTheme.MakeLabel(titel.ToUpperInvariant(), 10, FmTheme.TextSecondary));
        var zeile = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        zeile.AddThemeConstantOverride("separation", 8);
        zeile.AddChild(FmTheme.MakeZahl(wert, 30, farbe));
        if (einheit != "")
        {
            var e = FmTheme.MakeLabel(einheit, 12, FmTheme.TextSecondary);
            e.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            zeile.AddChild(e);
        }
        box.AddChild(zeile);
        tafel.AddChild(box);
        return tafel;
    }

    // ── Abteilungen ──────────────────────────────────────────────────────────

    private void ZeigeAbteilungen(StabModel stab)
    {
        Leere(_abteilungen);
        foreach (var abteilung in stab.Abteilungen)
        {
            _abteilungen.AddChild(Abteilungskarte(abteilung, stab));
        }
    }

    private Control Abteilungskarte(StabAbteilung abteilung, StabModel stab)
    {
        var karte = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
        };
        karte.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());
        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 18, 14);
        karte.AddChild(rand);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        rand.AddChild(box);

        var kopf = new HBoxContainer();
        var titel = FmTheme.MakeAbschnitt(Mehrzahl(abteilung.Anzeige), 20);
        titel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(titel);
        var besetzt = FmTheme.MakeLabel($"{abteilung.Mitarbeiter.Count} von {stab.MaxJeRolle}", 13,
            FmTheme.TextSecondary);
        besetzt.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        kopf.AddChild(besetzt);
        box.AddChild(kopf);

        var erklaerung = FmTheme.MakeLabel(Erklaerung(abteilung.Rolle), 12, FmTheme.TextGedimmt);
        erklaerung.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(erklaerung);

        if (abteilung.Mitarbeiter.Count == 0)
        {
            var leer = FmTheme.MakeLabel(Fehlt(abteilung.Rolle), 13, FmTheme.Danger);
            leer.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            box.AddChild(leer);
        }
        foreach (var person in abteilung.Mitarbeiter)
        {
            var knopf = Knopf("Entlassen", primaer: false);
            var p = person;
            knopf.Pressed += () => FrageEntlassen(p, stab.AbfindungWochen);
            box.AddChild(Personenzeile(p.Name, p.Qualitaet, p.Gehalt, knopf,
                p.Eingestellt is { } seit ? $"seit {seit:dd.MM.yyyy}" : ""));
        }

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        box.AddChild(sep);
        box.AddChild(FmTheme.MakeAbschnitt("Bewerber diesen Monat", 16));

        bool voll = abteilung.Mitarbeiter.Count >= stab.MaxJeRolle;
        foreach (var bewerber in abteilung.Bewerber)
        {
            var knopf = Knopf("Einstellen", primaer: true);
            knopf.Disabled = voll;
            if (voll) knopf.TooltipText = $"Mehr als {stab.MaxJeRolle} beschäftigt der Verein nicht.";
            var b = bewerber;
            knopf.Pressed += () => _ = StelleEin(abteilung.Rolle, b);
            box.AddChild(Personenzeile(b.Name, b.Qualitaet, b.Gehalt, knopf, ""));
        }
        return karte;
    }

    /// <summary>Name, Qualität als Balken und Zahl, Gehalt und die Aktion.</summary>
    private static Control Personenzeile(string name, int qualitaet, int gehalt, Button aktion, string zusatz)
    {
        var zeile = new PanelContainer();
        var stil = FmTheme.Flaeche(FmTheme.BgDark, 8, FmTheme.Border);
        stil.ContentMarginLeft = stil.ContentMarginRight = 12;
        stil.ContentMarginTop = stil.ContentMarginBottom = 6;
        zeile.AddThemeStyleboxOverride("panel", stil);

        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", 12);
        zeile.AddChild(reihe);

        var namen = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        namen.AddThemeConstantOverride("separation", 0);
        var n = FmTheme.MakeLabel(name, 14, FmTheme.TextPrimary);
        n.ClipText = true;
        n.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        namen.AddChild(n);
        if (zusatz != "") namen.AddChild(FmTheme.MakeLabel(zusatz, 11, FmTheme.TextGedimmt));
        namen.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        reihe.AddChild(namen);

        var balken = new ProgressBar
        {
            MinValue = 0, MaxValue = 100, Value = qualitaet,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(90, 8),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = "Qualität von 1 bis 100",
        };
        balken.AddThemeStyleboxOverride("fill", FmTheme.Flaeche(QualitaetsFarbe(qualitaet), 4));
        balken.AddThemeStyleboxOverride("background", FmTheme.Flaeche(FmTheme.BgHover, 4));
        reihe.AddChild(balken);

        var wert = FmTheme.MakeZahl(qualitaet.ToString(), 18, FmTheme.TextPrimary);
        wert.CustomMinimumSize = new Vector2(28, 0);
        wert.HorizontalAlignment = HorizontalAlignment.Right;
        wert.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        reihe.AddChild(wert);

        var g = FmTheme.MakeLabel(FmTheme.Gehalt(gehalt), 13, FmTheme.TextSecondary, HorizontalAlignment.Right);
        g.CustomMinimumSize = new Vector2(96, 0);
        g.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        g.TooltipText = Gehaltsformat.Titel;
        g.MouseFilter = MouseFilterEnum.Pass;
        reihe.AddChild(g);

        aktion.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        reihe.AddChild(aktion);
        return zeile;
    }

    private static Color QualitaetsFarbe(int qualitaet) => qualitaet switch
    {
        >= 80 => FmTheme.Gold,
        >= 60 => FmTheme.Success,
        >= 40 => FmTheme.TextNav,
        _     => FmTheme.TextGedimmt,
    };

    private static Button Knopf(string text, bool primaer)
    {
        var knopf = new Button { Text = text, CustomMinimumSize = new Vector2(104, 32) };
        if (primaer) FmTheme.ApplyPrimaryButton(knopf);
        else FmTheme.ApplySecondaryButton(knopf);
        return knopf;
    }

    private static string Mehrzahl(string rolle) => rolle switch
    {
        "Physiotherapeut" => "Physiotherapeuten",
        "Jugendscout"     => "Jugendscouts",
        _                 => rolle,
    };

    private static string Erklaerung(string rolle) => rolle switch
    {
        "CO_TRAINER" => "Leiten Einheiten mit an: Alle Spieler lernen im Training schneller. "
                        + "Der Beste zählt voll, der zweite halb.",
        "JUGENDSCOUT" => "Sichten in der Region und schlagen Talente vor, überwiegend für die C-Jugend. "
                         + "Ein besserer Scout findet öfter und bessere - einladen unter Jugend › Talente.",
        _            => "Behandeln und regenerieren: Die Spieler erholen sich zwischen den Spielen besser. "
                        + "Der Beste zählt voll, der zweite halb.",
    };

    private static string Fehlt(string rolle) => rolle switch
    {
        "CO_TRAINER" => "Noch kein Co-Trainer - das Training bringt zehn Prozent weniger als bei den anderen Vereinen.",
        "JUGENDSCOUT" => "Noch kein Jugendscout - der Verein findet keine Talente von außerhalb.",
        _            => "Noch kein Physiotherapeut - jeder Spieler erholt sich zwei Frischepunkte je Woche weniger.",
    };

    // ── Aktionen ─────────────────────────────────────────────────────────────

    private async Task StelleEin(string rolle, ScoutBewerber bewerber)
    {
        var antwort = await ApiClient.SendeAsync("POST",
            $"mitarbeiter/{GameState.Instance.VereinId}/{rolle}/bewerber/{bewerber.Nummer}");
        if (!Lebt) return;
        _statusLabel.Text = antwort.Ok
            ? $"{bewerber.Name} ist eingestellt."
            : antwort.Fehler ?? "Die Einstellung ist fehlgeschlagen.";
        await Lade();
    }

    private void FrageEntlassen(StabPerson person, int abfindungWochen)
    {
        long abfindung = (long)person.Gehalt * abfindungWochen;
        var menu = new PopupMenu();
        menu.AddItem($"{person.Name} entlassen?", -1);
        menu.SetItemDisabled(0, true);
        menu.AddSeparator();
        menu.AddItem($"Ja - Abfindung {FmTheme.Geld(abfindung)}", 1);
        menu.AddItem("Nein", 0);
        AddChild(menu);
        menu.IdPressed += async id =>
        {
            if (id != 1) return;
            var antwort = await ApiClient.SendeAsync("DELETE", $"mitarbeiter/person/{person.Id}");
            if (!Lebt) return;
            _statusLabel.Text = antwort.Ok ? $"{person.Name} entlassen." : antwort.Fehler ?? "Abgelehnt.";
            await Lade();
        };
        menu.PopupHide += menu.QueueFree;
        menu.Position = (Vector2I)GetGlobalMousePosition();
        menu.Popup();
    }

    private static void Leere(Node box)
    {
        foreach (var kind in box.GetChildren()) kind.QueueFree();
    }
}
