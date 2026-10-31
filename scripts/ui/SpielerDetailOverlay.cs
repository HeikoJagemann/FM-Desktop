#nullable enable
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Alles über einen eigenen Spieler: oben die Kennzahlen als Tafeln, darunter links die
/// Fähigkeiten, rechts Positionen, Torwartwerte und Stammdaten.
///
/// <para>Zwei Spalten statt einer langen Liste - so passt der Spieler ohne Bildlauf auf den
/// Schirm, und die Fähigkeiten stehen neben dem, was sie bewirken.</para>
/// </summary>
public partial class SpielerDetailOverlay : Overlaykarte
{
    /// Über wie viele Spieltage die Veränderung neben den Fähigkeiten zusammengefasst wird.
    private const int Spieltagsfenster = 10;

    private Spieler _s = null!;

    /// Entwicklung der letzten Spieltage; wird nachgeladen, die Karte steht vorher schon.
    private SpielerVerlauf? _verlauf;
    private List<KatalogEintrag> _bereiche = new();
    private VBoxContainer _inhalt = null!;

    public SpielerDetailOverlay() : base("", 960) { }

    private SpielerDetailOverlay(Spieler spieler) : base(spieler.Name, 960)
    {
        _s = spieler;
    }

    public static void Zeige(Node caller, Spieler spieler)
    {
        Oeffne(caller, new SpielerDetailOverlay(spieler), spieler.Id.ToString());
    }

    protected override void BaueInhalt(VBoxContainer inhalt)
    {
        string vertrag = _s.Gehalt > 0 ? $"  ·  Vertrag bis {_s.VertragBis + 1}" : "";
        SetzeUntertitel($"{KaderLabel(_s.Kader)}  ·  {_s.Nationalitaet}  ·  {_s.Alter} Jahre{vertrag}");

        _inhalt = new VBoxContainer();
        _inhalt.AddThemeConstantOverride("separation", 16);
        _inhalt.AddChild(FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary));
        inhalt.AddChild(_inhalt);
        _ = Lade();
    }

    private async Task Lade()
    {
        var verlaufTask = ApiClient.GetAsync<SpielerVerlauf>(
            $"training/spieler/{_s.Id}/verlauf?spieltage={Spieltagsfenster}");
        var planTask = ApiClient.GetAsync<TrainingsplanModel>($"training/{GameState.Instance.VereinId}");
        _verlauf = await verlaufTask;
        _bereiche = (await planTask)?.Bereiche ?? new List<KatalogEintrag>();
        if (!IsInstanceValid(this)) return;
        foreach (Node kind in _inhalt.GetChildren()) kind.QueueFree();

        _inhalt.AddChild(Kennzahlen());

        // So hoch, wie das Fenster erlaubt - im Vollbild ohne Bildlauf, im kleinen Fenster mit.
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, Mathf.Clamp(GetViewportRect().Size.Y - 400, 280, 500)),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _inhalt.AddChild(scroll);

        var spalten = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        spalten.AddThemeConstantOverride("separation", 16);
        scroll.AddChild(spalten);

        spalten.AddChild(Faehigkeiten());

        var rechts = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rechts.AddThemeConstantOverride("separation", 16);
        rechts.AddChild(Trainingsfokus());
        rechts.AddChild(Positionen());
        if (_s.IstTorwart) rechts.AddChild(Torwart());
        rechts.AddChild(Stammdaten());
        spalten.AddChild(rechts);
    }

    private static string KaderLabel(string kader) => kader switch
    {
        "JugendA" => "Jugend A",
        "JugendB" => "Jugend B",
        "JugendC" => "Jugend C",
        _         => kader,
    };

    // ── Kennzahlen ───────────────────────────────────────────────────────────

    /// <summary>Position, Stärke, Talent, Potenzial, Frische und Marktwert als Reihe von Tafeln.</summary>
    private Control Kennzahlen()
    {
        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", 10);

        reihe.AddChild(Positionstafel());
        reihe.AddChild(Tafel("Stärke", _s.BestPositionStaerke.ToString(), FmTheme.TextPrimary,
            Veraenderung(_verlauf?.StaerkeDifferenz ?? 0), _s.BestPositionErklaerung));
        reihe.AddChild(Tafel("Talent", _s.TalentSterneText, FmTheme.Gold, "",
            $"Talent {Spieler.SterneWort(_s.TalentSterne)} - wie leicht er Neues annimmt."));
        // Das Potenzial nur als Spanne - den exakten Wert liefert das Backend nicht aus, kein
        // Manager kennt das Maximum eines Spielers auf den Punkt.
        reihe.AddChild(Tafel("Potenzial", _s.PotenzialText, FmTheme.TextPrimary, _s.PotenzialSterneText,
            "Geschätzt - wie weit er sich noch entwickeln kann."));
        reihe.AddChild(Tafel("Form", _s.FormText, FmTheme.FormFarbe(_s.FormWert), "",
            $"Form {_s.FormWert} von 90 - gute Spiele heben sie, schwache senken sie. Im Spiel wirkt sie "
            + "bis zu ±6 % auf alle Fähigkeiten."));
        reihe.AddChild(Tafel("Frische", _s.Frische.ToString(), FmTheme.FrischeFarbe(_s.Frische), "",
            "Startwert der Kondition im nächsten Spiel."));
        reihe.AddChild(Tafel("Marktwert", FmTheme.Geld(_s.Wert), FmTheme.TextPrimary, ""));
        return reihe;
    }

    /// <summary>Die Hauptposition als Chip in der Farbe ihres Mannschaftsteils.</summary>
    private Control Positionstafel()
    {
        var gruppe = _s.Gruppe;
        var tafel = new PanelContainer { CustomMinimumSize = new Vector2(92, 0) };
        var stil = FmTheme.Flaeche(FmTheme.GruppeChip(gruppe), 10, FmTheme.GruppeFarbe(gruppe).Darkened(0.35f));
        stil.SetContentMarginAll(10);
        tafel.AddThemeStyleboxOverride("panel", stil);

        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 0);
        box.AddChild(FmTheme.MakeLabel("POSITION", 10, FmTheme.TextFuerGruppe(gruppe), HorizontalAlignment.Center));
        box.AddChild(FmTheme.MakeZahl(_s.HauptPosition, 30, FmTheme.TextFuerGruppe(gruppe)).Also(l =>
            l.HorizontalAlignment = HorizontalAlignment.Center));
        tafel.AddChild(box);
        return tafel;
    }

    private static Control Tafel(string titel, string wert, Color farbe, string zusatz, string tooltip = "")
    {
        var tafel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = tooltip };
        var stil = FmTheme.Flaeche(FmTheme.BgDark, 10, FmTheme.Border);
        stil.ContentMarginLeft = stil.ContentMarginRight = 14;
        stil.ContentMarginTop = stil.ContentMarginBottom = 10;
        tafel.AddThemeStyleboxOverride("panel", stil);

        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 0);
        box.AddChild(FmTheme.MakeLabel(titel.ToUpperInvariant(), 10, FmTheme.TextSecondary));

        var zeile = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        zeile.AddThemeConstantOverride("separation", 8);
        zeile.AddChild(FmTheme.MakeZahl(wert, 30, farbe));
        if (zusatz != "")
        {
            var z = FmTheme.MakeLabel(zusatz, 12, ZusatzFarbe(zusatz));
            z.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            zeile.AddChild(z);
        }
        box.AddChild(zeile);
        tafel.AddChild(box);
        return tafel;
    }

    /// <summary>Veränderungen grün oder rot, Sterne in Gold.</summary>
    private static Color ZusatzFarbe(string zusatz) =>
        zusatz.StartsWith('+') ? FmTheme.Success
      : zusatz.StartsWith('−') ? FmTheme.Danger
      : FmTheme.Gold;

    /// <summary>"+3", "−2" oder leer.</summary>
    private static string Veraenderung(int differenz) =>
        differenz > 0 ? $"+{differenz}" : differenz < 0 ? $"−{-differenz}" : "";

    // ── Fähigkeiten ──────────────────────────────────────────────────────────

    private Control Faehigkeiten()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 7);
        v.AddChild(Legende());
        v.AddChild(Fertigkeit("Pass", _s.Pass, "PASS"));
        v.AddChild(Fertigkeit("Ballkontrolle", _s.Ballkontrolle, "BALLKONTROLLE"));
        v.AddChild(Fertigkeit("Schusstechnik", _s.Schusstechnik, "SCHUSSTECHNIK"));
        v.AddChild(Fertigkeit("Schussstärke", _s.Schussstaerke, "SCHUSSSTAERKE"));
        v.AddChild(Fertigkeit("Dribbling", _s.Dribbling, "DRIBBLING"));
        v.AddChild(Fertigkeit("Kopfball", _s.Kopfball, "KOPFBALL"));
        v.AddChild(Fertigkeit("Zweikampf", _s.Zweikampf, "ZWEIKAMPF"));
        v.AddChild(Fertigkeit("Stellungsspiel", _s.Stellungsspiel, "STELLUNGSSPIEL"));
        v.AddChild(Fertigkeit("Entscheidungen", _s.Entscheidungen, "ENTSCHEIDUNGEN"));
        v.AddChild(Fertigkeit("Führungsqualität", _s.Fuehrungsqualitaet, "FUEHRUNGSQUALITAET"));
        v.AddChild(Fertigkeit("Disziplin", _s.Disziplin, "DISZIPLIN"));
        v.AddChild(Fertigkeit("Schnelligkeit", _s.Schnelligkeit, "SCHNELLIGKEIT"));
        v.AddChild(Fertigkeit("Ausdauer", _s.Ausdauer, "AUSDAUER"));
        v.AddChild(AttributZeile("Linker Fuß", _s.LinkerFuss));
        v.AddChild(AttributZeile("Rechter Fuß", _s.RechterFuss));
        if (_verlauf != null && _verlauf.BisSpieltag > 0)
        {
            v.AddChild(FmTheme.MakeLabel($"Veränderungen seit dem {_verlauf.VonSpieltag}. Spieltag",
                11, FmTheme.TextGedimmt));
        }

        var karte = Karte("Fähigkeiten", v);
        karte.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        karte.SizeFlagsStretchRatio = 1.25f;
        return karte;
    }

    private Control Torwart()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 7);
        v.AddChild(Sternezeile("Talent (TW)", _s.TalentTW));
        v.AddChild(Fertigkeit("Strafraumbeherrschung", _s.Strafraumbeherrschung, "STRAFRAUMBEHERRSCHUNG"));
        v.AddChild(Fertigkeit("Fangsicherheit", _s.Fangsicherheit, "FANGSICHERHEIT"));
        v.AddChild(Fertigkeit("Reflexe", _s.Reflexe, "REFLEXE"));
        return Karte("Torwart", v);
    }

    /// <summary>Was die Punkte vor den Fähigkeiten bedeuten.</summary>
    private Control Legende()
    {
        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", 4);
        reihe.AddChild(Symbol.Neu(Symbol.Art.Punkt, FmTheme.Accent, 14));
        reihe.AddChild(FmTheme.MakeLabel($"entscheidend als {_s.HauptPosition}", 11, FmTheme.TextSecondary));
        reihe.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0) });
        reihe.AddChild(Symbol.Neu(Symbol.Art.Punkt, FmTheme.TextSecondary, 14));
        reihe.AddChild(FmTheme.MakeLabel("spielt mit", 11, FmTheme.TextSecondary));
        return reihe;
    }

    /// <summary>Eine trainierbare Fähigkeit - mit Rolle für die Position und Veränderung.</summary>
    private Control Fertigkeit(string name, int wert, string schluessel) =>
        AttributZeile(name, wert, _verlauf?.DifferenzFuer(schluessel) ?? 0, _s.RolleVon(name));

    /// <param name="differenz">
    /// Veränderung über die letzten Spieltage. Ungleich 0 steht sie hinter dem Wert - so ist auf
    /// einen Blick erkennbar, woran der Spieler zuletzt gewachsen ist.
    /// </param>
    private static Control AttributZeile(string label, int wert, int differenz = 0,
                                         Attributrolle rolle = Attributrolle.Nebensaechlich)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        // Primäre Fähigkeiten hervorheben, nebensächliche zurücknehmen: So ist auf einen Blick zu
        // sehen, worauf es bei diesem Spieler ankommt.
        row.AddChild(rolle == Attributrolle.Nebensaechlich
            ? new Control { CustomMinimumSize = new Vector2(12, 12) }
            : Symbol.Neu(Symbol.Art.Punkt,
                rolle == Attributrolle.Primaer ? FmTheme.Accent : FmTheme.TextSecondary, 12));

        var l = FmTheme.MakeLabel(label, 13, rolle switch
        {
            Attributrolle.Primaer   => FmTheme.TextPrimary,
            Attributrolle.Sekundaer => FmTheme.TextNav,
            _                       => FmTheme.TextGedimmt,
        });
        l.CustomMinimumSize = new Vector2(158, 0);
        row.AddChild(l);

        var bar = new ProgressBar
        {
            MinValue = 0, MaxValue = 100, Value = wert,
            ShowPercentage = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, 8),
        };
        bar.AddThemeStyleboxOverride("fill", FmTheme.Flaeche(FarbeFuerWert(wert), 4));
        bar.AddThemeStyleboxOverride("background", FmTheme.Flaeche(FmTheme.BgHover, 4));
        row.AddChild(bar);

        var wertLabel = FmTheme.MakeZahl(wert.ToString(), 17, FmTheme.TextPrimary);
        wertLabel.HorizontalAlignment = HorizontalAlignment.Right;
        wertLabel.CustomMinimumSize = new Vector2(28, 0);
        row.AddChild(wertLabel);

        // Die Spalte bleibt auch ohne Veränderung stehen, damit die Werte nicht springen.
        var trend = FmTheme.MakeZahl(Veraenderung(differenz), 15,
            differenz > 0 ? FmTheme.Success : FmTheme.Danger);
        trend.HorizontalAlignment = HorizontalAlignment.Right;
        trend.CustomMinimumSize = new Vector2(30, 0);
        row.AddChild(trend);

        return row;
    }

    /// <summary>Ein Talent als Sterne statt als Zahl, bündig mit den Fähigkeitszeilen.</summary>
    private static Control Sternezeile(string label, int talent)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new Control { CustomMinimumSize = new Vector2(12, 12) });
        var l = FmTheme.MakeLabel(label, 13, FmTheme.TextPrimary);
        l.CustomMinimumSize = new Vector2(158, 0);
        row.AddChild(l);
        int sterne = Spieler.Sterne(talent);
        var s = FmTheme.MakeLabel(Spieler.SterneText(sterne), 15, FmTheme.Gold);
        s.TooltipText = $"Talent {Spieler.SterneWort(sterne)}";
        s.MouseFilter = MouseFilterEnum.Pass;
        row.AddChild(s);
        return row;
    }

    private static Color FarbeFuerWert(int wert) => wert switch
    {
        >= 80 => FmTheme.Gold,
        >= 65 => FmTheme.Success,
        >= 50 => FmTheme.TextNav,
        _     => FmTheme.TextGedimmt,
    };

    // ── Positionen und Stammdaten ────────────────────────────────────────────

    /// <summary>Die drei besten Positionen mit Stärke; ~ und ? zeigen, wie vertraut sie ihm sind.</summary>
    private Control Positionen()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        if (_s.Top3Positionen is { Count: > 0 } positionen)
        {
            foreach (var p in positionen)
            {
                var zeile = new HBoxContainer { TooltipText = p.Erklaerung };
                zeile.AddThemeConstantOverride("separation", 10);
                var gruppe = PositionsgruppeHelfer.Von(p.Position);
                var chip = FmTheme.MakeLabel(p.Position, 12, FmTheme.TextFuerGruppe(gruppe), HorizontalAlignment.Center);
                chip.AddThemeFontOverride("font", FmTheme.SchriftUi(700));
                chip.AddThemeStyleboxOverride("normal", FmTheme.Flaeche(FmTheme.GruppeChip(gruppe), 5));
                chip.CustomMinimumSize = new Vector2(40, 22);
                chip.VerticalAlignment = VerticalAlignment.Center;
                chip.MouseFilter = MouseFilterEnum.Pass;
                zeile.AddChild(chip);
                var vertraut = FmTheme.MakeLabel(Vertrautheit(p.Zeichen), 13, FmTheme.TextSecondary);
                vertraut.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                vertraut.MouseFilter = MouseFilterEnum.Pass;
                zeile.AddChild(vertraut);
                var staerke = FmTheme.MakeZahl(p.Staerke.ToString(), 18, FmTheme.TextPrimary);
                staerke.MouseFilter = MouseFilterEnum.Pass;
                zeile.AddChild(staerke);
                v.AddChild(zeile);
            }
        }
        else
        {
            v.AddChild(InfoZeile(_s.Position, _s.Staerke.ToString()));
        }
        return Karte("Beste Positionen", v);
    }

    private static string Vertrautheit(string zeichen) => zeichen switch
    {
        "~" => "teilweise vertraut",
        "?" => "ungewohnt",
        _   => "vertraut",
    };

    private Control Stammdaten()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);
        v.AddChild(InfoZeile("Geburtsdatum", _s.Geburtsdatum.ToString("dd.MM.yyyy")));
        v.AddChild(InfoZeile("Nationalität", _s.Nationalitaet));
        v.AddChild(InfoZeile("Kader", KaderLabel(_s.Kader)));
        v.AddChild(InfoZeile("Spiele in dieser Saison", _s.SpieleInSaison.ToString()));
        if (_s.Gehalt > 0)
        {
            v.AddChild(InfoZeile(FmTheme.GehaltTitel, FmTheme.Gehalt(_s.Gehalt)));
            v.AddChild(InfoZeile("Vertrag bis", $"Saison {_s.VertragBis}/{_s.VertragBis + 1}"));
        }
        return Karte("Stammdaten", v);
    }

    // ── Bausteine ────────────────────────────────────────────────────────────

    // ── Trainingsfokus ───────────────────────────────────────────────────────

    /// <summary>
    /// Der persönliche Schwerpunkt: was der Spieler zusätzlich zum Mannschaftstraining übt. Gilt
    /// ab der nächsten Trainingswoche - wie auf der Trainingsseite, nur ohne den Umweg dorthin.
    /// </summary>
    private Control Trainingsfokus()
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 6);

        var auswahl = new OptionButton { CustomMinimumSize = new Vector2(0, 36) };
        auswahl.AddItem("Kein persönlicher Schwerpunkt", 0);
        int gewaehlt = 0;
        for (int i = 0; i < _bereiche.Count; i++)
        {
            auswahl.AddItem(_bereiche[i].Anzeige, i + 1);
            if (_bereiche[i].Schluessel == _s.IndividualFokus) gewaehlt = i + 1;
        }
        auswahl.Select(gewaehlt);
        v.AddChild(auswahl);

        var erklaerung = FmTheme.MakeLabel(FokusText(gewaehlt), 12, FmTheme.TextSecondary);
        erklaerung.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        v.AddChild(erklaerung);

        auswahl.ItemSelected += async index =>
        {
            string? fokus = index == 0 ? null : _bereiche[(int)index - 1].Schluessel;
            erklaerung.Text = FokusText((int)index);
            await ApiClient.PutAsync<object, object>(
                $"training/spieler/{_s.Id}/fokus", new { fokus = fokus ?? "" });
            if (!IsInstanceValid(this)) return;
            _s.IndividualFokus = fokus;
            erklaerung.Text = FokusText((int)index) + " Gilt ab der nächsten Trainingswoche.";
        };
        return Karte("Trainingsfokus", v);
    }

    private string FokusText(int index) => index == 0 || index > _bereiche.Count
        ? "Er trainiert nur mit der Mannschaft."
        : $"Schult zusätzlich: {_bereiche[index - 1].Beschreibung}.";

    private static PanelContainer Karte(string titel, Control inhalt)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 16, 14);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        margin.AddChild(vbox);

        vbox.AddChild(FmTheme.MakeAbschnitt(titel, 18));
        vbox.AddChild(inhalt);
        return panel;
    }

    private static Control InfoZeile(string label, string wert)
    {
        var row = new HBoxContainer();
        var l = FmTheme.MakeLabel(label, 13, FmTheme.TextSecondary);
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(l);
        row.AddChild(FmTheme.MakeLabel(wert, 13, FmTheme.TextPrimary));
        return row;
    }
}

file static class NodeExtensions
{
    public static T Also<T>(this T node, System.Action<T> configure) where T : Node
    {
        configure(node);
        return node;
    }
}
