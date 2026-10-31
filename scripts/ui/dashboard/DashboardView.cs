#nullable enable
using Godot;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Dashboard;

/// <summary>
/// Die Startseite: Hier landet der Manager beim Spielstart und nach jedem Kalenderschritt.
///
/// <para>Drei Spalten über die ganze Fensterhöhe, ohne Bildlauf der Seite: links, was ansteht und
/// was passiert ist - die nächste Partie mit dem Kräfteverhältnis und die Meldungen. In der Mitte
/// die Tabelle. Rechts der letzte Spieltag und wer sich im Training entwickelt.</para>
///
/// <para>Die Stärke des Gegners ist eine Spanne aus den Scoutberichten, gemittelt über seine
/// voraussichtliche Elf. Wer ihn beobachten lässt oder gegen ihn spielt, sieht ihn genauer.</para>
/// </summary>
public partial class DashboardView : Control
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private Label _kopfLabel = null!;
    private VBoxContainer _naechstesSpiel = null!;
    private VBoxContainer _meldungen = null!;
    private Label _spieltagLabel = null!;
    private VBoxContainer _ergebnisse = null!;
    private FmGrid<TabellenEintrag> _tabelle = null!;
    private VBoxContainer _trainingWoche = null!;
    private VBoxContainer _trainingSaison = null!;
    private Label _trainingWocheLabel = null!;

    private bool Lebt => IsInstanceValid(this) && !IsQueuedForDeletion();

    public override async void _Ready()
    {
        BuildUI();
        await Lade();
    }

    // ── Aufbau ───────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Keine Bildlaufleiste: Die Seite teilt sich die Fensterhöhe auf. Nur die Meldungen haben
        // mehr, als je auf eine Seite passt - sie laufen in ihrer eigenen Karte.
        var seite = new VBoxContainer();
        seite.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        seite.AddThemeConstantOverride("separation", 12);
        AddChild(seite);

        // Verein, Liga, Datum und Kontostand stehen schon in der Kopfleiste - hier nur der Titel.
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 16);
        seite.AddChild(kopf);
        kopf.AddChild(FmTheme.MakeTitel("Übersicht"));
        _kopfLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        _kopfLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        kopf.AddChild(_kopfLabel);

        var spalten = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        spalten.AddThemeConstantOverride("separation", 12);
        seite.AddChild(spalten);

        // Links: was ansteht und was passiert ist.
        var links = Saeule(spalten, 1.25f);

        _naechstesSpiel = new VBoxContainer();
        _naechstesSpiel.AddThemeConstantOverride("separation", 8);
        links.AddChild(Karte("Nächstes Spiel", _naechstesSpiel));

        _meldungen = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _meldungen.AddThemeConstantOverride("separation", 10);
        var meldungsScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        // Rechts Luft für die Bildlaufleiste, sonst läge sie über den Daten.
        var meldungsRand = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        FmTheme.SetMargin(meldungsRand, 0, 14, 0, 0);
        meldungsRand.AddChild(_meldungen);
        meldungsScroll.AddChild(meldungsRand);
        links.AddChild(Karte("Ereignisse", meldungsScroll, fuellen: true));

        // Mitte: wo der Verein steht.
        var mitte = Saeule(spalten, 1f);
        _tabelle = new FmGrid<TabellenEintrag>(TabellenSpalten.Kompakt)
        {
            Zeilenhoehe = 28,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        mitte.AddChild(Karte("Tabelle", _tabelle, fuellen: true));

        // Rechts: der letzte Spieltag und wer sich im Training entwickelt.
        var rechts = Saeule(spalten, 1f);

        var spieltag = new VBoxContainer();
        spieltag.AddThemeConstantOverride("separation", 4);
        _spieltagLabel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        spieltag.AddChild(_spieltagLabel);
        _ergebnisse = new VBoxContainer();
        _ergebnisse.AddThemeConstantOverride("separation", 2);
        spieltag.AddChild(_ergebnisse);
        rechts.AddChild(Karte("Letzter Spieltag", spieltag));

        // Woche und Saison teilen sich eine Karte - nebeneinander oder untereinander kosteten sie
        // den Platz, den die Seite nicht hat.
        var training = new VBoxContainer();
        training.AddThemeConstantOverride("separation", 4);
        var woche = new VBoxContainer();
        woche.AddThemeConstantOverride("separation", 4);
        _trainingWocheLabel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        woche.AddChild(_trainingWocheLabel);
        _trainingWoche = new VBoxContainer();
        woche.AddChild(_trainingWoche);
        training.AddChild(woche);
        _trainingSaison = new VBoxContainer { Visible = false };
        training.AddChild(_trainingSaison);

        var zeitraum = Segmentschalter.Neu(new[] { "Woche", "Saison" }, 0, new Vector2(70, 28), 13);
        zeitraum.Gewaehlt += i =>
        {
            woche.Visible = i == 0;
            _trainingSaison.Visible = i == 1;
        };
        rechts.AddChild(Karte("Trainingsfortschritt", training, zeitraum, fuellen: true));
    }

    /// <summary>Eine Spalte der Seite; das Gewicht bestimmt ihren Anteil an der Breite.</summary>
    private static VBoxContainer Saeule(HBoxContainer spalten, float gewicht)
    {
        var saeule = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = gewicht,
        };
        saeule.AddThemeConstantOverride("separation", 12);
        spalten.AddChild(saeule);
        return saeule;
    }

    /// <summary>
    /// Ein Abschnitt mit Überschrift auf eigener Fläche. Mit <paramref name="fuellen"/> nimmt er
    /// die restliche Höhe seiner Spalte ein, und ein Inhalt mit ExpandFill wächst mit.
    /// </summary>
    private static PanelContainer Karte(string titel, Control inhalt, Control? kopfZusatz = null,
                                        bool fuellen = false)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (fuellen) panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());
        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 16, 12);
        panel.AddChild(rand);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        rand.AddChild(box);

        var kopf = new HBoxContainer();
        var ueberschrift = FmTheme.MakeAbschnitt(titel, 18);
        ueberschrift.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ueberschrift.ClipText = true;
        ueberschrift.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        ueberschrift.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        kopf.AddChild(ueberschrift);
        if (kopfZusatz != null) kopf.AddChild(kopfZusatz);
        box.AddChild(kopf);
        box.AddChild(inhalt);
        return panel;
    }

    // ── Laden ────────────────────────────────────────────────────────────────

    private async Task Lade()
    {
        long vereinId = GameState.Instance.VereinId;
        var dashboard = await ApiClient.GetAsync<DashboardModel>($"dashboard/{vereinId}");
        if (!Lebt) return;
        if (dashboard == null)
        {
            _kopfLabel.Text = "Die Übersicht konnte nicht geladen werden.";
            return;
        }

        _kopfLabel.Visible = false;
        ZeigeNaechstesSpiel(dashboard.NaechstesSpiel);
        ZeigeMeldungen(dashboard.Meldungen);
        ZeigeTabelle(dashboard.Tabelle, vereinId);
        ZeigeErgebnisse(dashboard.LetzterSpieltag, dashboard.LetzteErgebnisse);
        _trainingWocheLabel.Text = dashboard.Trainingswoche is { } w ? $"Woche bis {w:dd.MM.yyyy}" : "";
        ZeigeTraining(_trainingWoche, dashboard.TrainingWoche);
        ZeigeTraining(_trainingSaison, dashboard.TrainingSaison);

        // Gezeigt ist gezeigt: Beim nächsten Besuch sind nur die seither eingegangenen neu.
        if (dashboard.Meldungen.Exists(m => m.Neu))
            await ApiClient.SendeAsync("POST", "dashboard/gelesen");
    }

    private void ZeigeNaechstesSpiel(NaechstesSpiel? spiel)
    {
        Leere(_naechstesSpiel);
        if (spiel == null)
        {
            _naechstesSpiel.AddChild(FmTheme.MakeLabel("In dieser Saison steht keine Partie mehr an.", 13, FmTheme.TextSecondary));
            return;
        }

        string wann = spiel.Datum is { } d
            ? d.ToString("ddd, dd.MM.yyyy", Deutsch) + (spiel.Uhrzeit is { } u ? $", {u:HH\\:mm} Uhr" : "")
            : "";
        _naechstesSpiel.AddChild(FmTheme.MakeLabel(
            $"{spiel.Spieltag}. Spieltag  ·  {wann}  ·  {(spiel.Heimspiel ? "Heimspiel" : "Auswärtsspiel")}",
            12, FmTheme.TextSecondary));

        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 16);
        _naechstesSpiel.AddChild(zeile);

        string eigenerName = spiel.Heimspiel ? spiel.Heim : spiel.Gast;
        string gegnerName = spiel.Heimspiel ? spiel.Gast : spiel.Heim;
        var eigene = Mannschaft(eigenerName, spiel.EigenerPlatz, Zahl(spiel.EigeneStaerke),
            FmTheme.Accent, "Stärke der aufgestellten Elf");
        var gegner = Mannschaft(gegnerName, spiel.GegnerPlatz,
            $"{Zahl(spiel.GegnerStaerkeVon)} – {Zahl(spiel.GegnerStaerkeBis)}", FmTheme.TextPrimary,
            "Voraussichtliche Elf, geschätzt aus den Scoutberichten");

        // Heimmannschaft links, wie auf jedem Spielplan.
        zeile.AddChild(spiel.Heimspiel ? eigene : gegner);
        var vs = FmTheme.MakeLabel("–", 22, FmTheme.TextGedimmt);
        vs.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        zeile.AddChild(vs);
        zeile.AddChild(spiel.Heimspiel ? gegner : eigene);

        string bekannt = spiel.GegnerBeobachtet == 0
            ? "Noch kein Spieler der gegnerischen Elf beobachtet - nur der öffentliche Eindruck. "
              + "Ein Scout macht die Spanne enger."
            : $"{spiel.GegnerBeobachtet} von {spiel.GegnerSpieler} Spielern der gegnerischen Elf beobachtet.";
        var hinweis = FmTheme.MakeLabel(bekannt, 12, FmTheme.TextGedimmt);
        hinweis.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _naechstesSpiel.AddChild(hinweis);
    }

    private static Control Mannschaft(string name, int? platz, string staerke, Color farbe, string tooltip)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 2);
        var titel = FmTheme.MakeLabel(name, 16, FmTheme.TextPrimary);
        titel.ClipText = true;
        titel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        box.AddChild(titel);
        box.AddChild(FmTheme.MakeLabel(platz is { } p ? $"{p}. Platz" : "", 12, FmTheme.TextSecondary));
        var zahl = FmTheme.MakeZahl(staerke, 24, farbe);
        zahl.TooltipText = tooltip;
        zahl.MouseFilter = MouseFilterEnum.Pass;
        box.AddChild(zahl);
        return box;
    }

    private void ZeigeMeldungen(List<Meldung> meldungen)
    {
        Leere(_meldungen);
        if (meldungen.Count == 0)
        {
            _meldungen.AddChild(FmTheme.MakeLabel("Noch nichts passiert.", 13, FmTheme.TextSecondary));
            return;
        }
        foreach (var m in meldungen)
        {
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 10);

            zeile.AddChild(Rubrikschild(m.Rubrik));

            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            text.AddThemeConstantOverride("separation", 1);
            var kopf = new HBoxContainer();
            kopf.AddThemeConstantOverride("separation", 8);
            // Gekürzt statt umbrochen: Im schmalen Fenster darf der Titel die Spalte nicht verbreitern.
            var titel = FmTheme.MakeLabel(m.Titel, 13, m.Neu ? FmTheme.TextPrimary : FmTheme.TextSecondary);
            titel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            titel.ClipText = true;
            titel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            kopf.AddChild(titel);
            if (m.Neu) kopf.AddChild(FmTheme.MakeLabel("NEU", 10, FmTheme.Accent));
            kopf.AddChild(FmTheme.MakeLabel(m.Datum.ToString("dd.MM.", Deutsch), 11, FmTheme.TextGedimmt));
            text.AddChild(kopf);

            // Einzeilig, damit viele Meldungen auf die Karte passen; der ganze Text im Tooltip.
            var inhalt = FmTheme.MakeLabel(m.Text, 12, FmTheme.TextGedimmt);
            inhalt.ClipText = true;
            inhalt.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            inhalt.TooltipText = m.Text;
            inhalt.MouseFilter = MouseFilterEnum.Pass;
            text.AddChild(inhalt);
            zeile.AddChild(text);

            _meldungen.AddChild(zeile);
        }
    }

    private void ZeigeTabelle(List<TabellenEintrag> tabelle, long vereinId)
    {
        _tabelle.Zeilenfarbe = TabellenSpalten.Zeilenfarbe(vereinId, tabelle.Count);
        _tabelle.Zeige(tabelle);
    }

    private void ZeigeErgebnisse(int spieltag, List<Ergebnis> ergebnisse)
    {
        Leere(_ergebnisse);
        _spieltagLabel.Text = spieltag > 0 ? $"{spieltag}. Spieltag" : "Noch kein Spieltag gespielt.";
        foreach (var e in ergebnisse)
        {
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 8);
            Color farbe = e.Eigenes ? FmTheme.Accent : FmTheme.TextPrimary;

            var heim = FmTheme.MakeLabel(e.Heim, 12, farbe);
            heim.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            heim.HorizontalAlignment = HorizontalAlignment.Right;
            heim.ClipText = true;
            heim.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            zeile.AddChild(heim);

            var tore = FmTheme.MakeZahl(e.ToreText, 13, farbe);
            tore.CustomMinimumSize = new Vector2(44, 0);
            tore.HorizontalAlignment = HorizontalAlignment.Center;
            zeile.AddChild(tore);

            var gast = FmTheme.MakeLabel(e.Gast, 12, farbe);
            gast.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            gast.ClipText = true;
            gast.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            zeile.AddChild(gast);

            _ergebnisse.AddChild(zeile);
        }
    }

    private static void ZeigeTraining(VBoxContainer box, List<Trainingsfortschritt> liste)
    {
        Leere(box);
        if (liste.Count == 0)
        {
            box.AddChild(FmTheme.MakeLabel("Noch kein Fortschritt.", 13, FmTheme.TextSecondary));
            return;
        }
        int rang = 1;
        foreach (var f in liste)
        {
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 10);
            zeile.AddChild(Spalte($"{rang++}.", 24, FmTheme.TextGedimmt));
            zeile.AddChild(Spalte(f.Position, 34, FmTheme.TextFuerGruppe(PositionsgruppeHelfer.Von(f.Position))));
            var name = FmTheme.MakeLabel($"{f.Name} ({f.Alter})", 13, FmTheme.TextPrimary);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.ClipText = true;
            zeile.AddChild(name);
            var punkte = Spalte($"+{f.Punkte}", 44, FmTheme.Success);
            punkte.TooltipText = "Fähigkeitspunkte aus Training und Spielpraxis";
            punkte.MouseFilter = MouseFilterEnum.Pass;
            zeile.AddChild(punkte);
            int differenz = f.StaerkeJetzt - f.StaerkeVorher;
            var staerke = Spalte($"{f.StaerkeVorher} → {f.StaerkeJetzt}", 70,
                differenz > 0 ? FmTheme.Success : FmTheme.TextSecondary);
            staerke.TooltipText = "Stärke auf seiner Position vorher und jetzt";
            staerke.MouseFilter = MouseFilterEnum.Pass;
            zeile.AddChild(staerke);
            box.AddChild(zeile);
        }
    }

    private static Label Spalte(string text, int breite, Color farbe)
    {
        var label = FmTheme.MakeLabel(text, 13, farbe);
        label.CustomMinimumSize = new Vector2(breite, 0);
        return label;
    }

    /// <summary>Kleines Schild mit der Rubrik einer Meldung - gleich breit, damit die Texte untereinander stehen.</summary>
    private static Control Rubrikschild(string rubrik)
    {
        var schild = new PanelContainer
        {
            CustomMinimumSize = new Vector2(78, 0),
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
        };
        var stil = FmTheme.Flaeche(FmTheme.BgHover, 6);
        stil.ContentMarginTop = stil.ContentMarginBottom = 2;
        schild.AddThemeStyleboxOverride("panel", stil);
        var text = FmTheme.MakeLabel(rubrik.ToUpperInvariant(), 10, FmTheme.TextNav, HorizontalAlignment.Center);
        text.AddThemeFontOverride("font", FmTheme.SchriftUi(700));
        schild.AddChild(text);
        return schild;
    }

    private static string Zahl(double wert) => wert.ToString("0.0", Deutsch);

    private static void Leere(Node box)
    {
        foreach (var kind in box.GetChildren()) kind.QueueFree();
    }
}
