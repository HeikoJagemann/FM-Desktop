#nullable enable
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Vertragsverhandlung - mit einem eigenen Spieler über die Verlängerung oder mit einem Neuzugang,
/// dessen Ablöse schon geklärt ist. Scheitert das Gespräch mit einem Neuzugang, platzt der Wechsel.
///
/// <para>Er nennt Gehalt und Laufzeit, du bietest. Er kommt dir entgegen, aber nie unter das, was
/// er sich wert ist. Jedes Angebot kostet Stimmung - umso mehr, je weiter es unter seiner Forderung
/// liegt. Ist sie aufgebraucht, bricht er ab. Gerechnet wird im Server; früher lief statt der
/// Stimmung eine Uhr über sechzig Sekunden.</para>
///
/// <para>Gehälter erscheinen in der eingestellten Periode (Woche, Monat oder Jahr); gerechnet
/// wird im Server immer mit dem Wochengehalt, auf volle Euro.</para>
/// </summary>
public partial class VertragsverhandlungDialog : Overlaykarte
{
    private long _spielerId;
    private string _name = "";
    /// <summary>Der eigene Spieler bei einer Verlängerung.</summary>
    private Spieler? _spieler;
    /// <summary>Das Transferangebot bei einem Neuzugang.</summary>
    private TransferangebotModel? _angebot;
    private Action? _geaendert;
    private Verhandlung? _stand;

    private Label _stimmungZahl = null!;
    private Label _stimmungWort = null!;
    private ProgressBar _stimmung = null!;
    private Label _forderung = null!;
    private Label _forderungLaufzeit = null!;
    private SpinBox _gehalt = null!;
    private HSlider _regler = null!;
    private Label _gehaltText = null!;
    private Segmentschalter? _laufzeit;
    private HBoxContainer _laufzeitPlatz = null!;
    private Label _laufzeitText = null!;
    private VBoxContainer _verlauf = null!;
    private ScrollContainer _verlaufScroll = null!;
    private Button _bieten = null!;
    private Button _erfuellen = null!;
    private Button _beenden = null!;
    private Label _hinweis = null!;
    /// <summary>Verhindert, dass sich Feld und Regler beim Abgleich gegenseitig anstoßen.</summary>
    private bool _gleichtAb;

    public VertragsverhandlungDialog() : base("Vertragsverhandlung", 900)
    {
        SchliesstBeiKlickDaneben = false;
    }

    /// <param name="geaendert">wenn ein neuer Vertrag zustande kam</param>
    public static void Zeige(Node caller, Spieler spieler, Action? geaendert = null)
    {
        Oeffne(caller, new VertragsverhandlungDialog
        {
            _spielerId = spieler.Id, _name = spieler.Name, _spieler = spieler, _geaendert = geaendert,
        }, spieler.Id.ToString());
    }

    /// <summary>Gespräch mit einem Neuzugang, dessen Ablöse geklärt ist.</summary>
    /// <param name="geaendert">wenn das Gespräch zu Ende ist - der Wechsel steht oder ist geplatzt</param>
    public static void ZeigeWechsel(Node caller, TransferangebotModel angebot, Action? geaendert = null)
    {
        Oeffne(caller, new VertragsverhandlungDialog
        {
            _spielerId = angebot.SpielerId, _name = angebot.SpielerName, _angebot = angebot, _geaendert = geaendert,
        }, angebot.SpielerId.ToString());
    }

    private bool Wechsel => _angebot != null;

    // ── Gehalt in der eingestellten Periode ──────────────────────────────────

    private static double Faktor => Gehaltsformat.Periode switch
    {
        GehaltsPeriode.Monat => 52.0 / 12.0,
        GehaltsPeriode.Jahr  => 52.0,
        _                    => 1.0,
    };

    /// <summary>
    /// Kleinster Schritt in der Anzeige - ein ganzes Vielfaches eines Euros Wochengehalt, damit die
    /// angezeigte Zahl genau das ist, was beim Server ankommt: Jahr 52 € (1 € je Woche), Monat 13 €
    /// (3 € je Woche), Woche 1 €. Früher waren es im Jahr 1.000 € - in der Oberliga musste man die
    /// Forderung so oft deutlich überbieten.
    /// </summary>
    private static double Schritt => Gehaltsformat.Periode switch
    {
        GehaltsPeriode.Monat => 13,
        GehaltsPeriode.Jahr  => 52,
        _                    => 1,
    };

    private static long ZuWoche(double angezeigt) => (long)Math.Round(angezeigt / Faktor);
    private static double AusWoche(long woche) => Math.Round(woche * Faktor);

    // ── Aufbau ───────────────────────────────────────────────────────────────

    protected override void BaueInhalt(VBoxContainer inhalt)
    {
        SetzeUntertitel($"{_name}  ·  Gespräch wird vorbereitet …");

        // Die Stimmung - sie ersetzt die Uhr.
        var stimmungZeile = new HBoxContainer();
        stimmungZeile.AddThemeConstantOverride("separation", 14);
        _stimmungZahl = FmTheme.MakeZahl("–", 40, FmTheme.Success);
        _stimmungZahl.CustomMinimumSize = new Vector2(70, 0);
        stimmungZeile.AddChild(_stimmungZahl);
        var stimmungSpalte = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter };
        stimmungSpalte.AddThemeConstantOverride("separation", 4);
        _stimmungWort = FmTheme.MakeLabel("STIMMUNG", 11, FmTheme.TextSecondary);
        stimmungSpalte.AddChild(_stimmungWort);
        _stimmung = new ProgressBar
        {
            MinValue = 0, MaxValue = 100, Value = 100, ShowPercentage = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 12),
        };
        _stimmung.AddThemeStyleboxOverride("background", FmTheme.Flaeche(FmTheme.BgDark, 6, FmTheme.Border));
        _stimmung.AddThemeStyleboxOverride("fill", FmTheme.Flaeche(FmTheme.Success, 6));
        stimmungSpalte.AddChild(_stimmung);
        stimmungZeile.AddChild(stimmungSpalte);
        stimmungZeile.TooltipText = "Wie das Gespräch läuft. Jedes Angebot kostet Stimmung - umso mehr, je weiter "
                                    + "es unter seiner Forderung liegt; ein beleidigendes kostet viel. Bei 0 bricht er ab.";
        inhalt.AddChild(stimmungZeile);

        // Forderung links, Angebot rechts
        var spalten = new HBoxContainer();
        spalten.AddThemeConstantOverride("separation", 16);
        inhalt.AddChild(spalten);

        var (fordertKarte, fordert) = Karte("Er fordert");
        _forderung = FmTheme.MakeZahl("–", 30, FmTheme.TextPrimary);
        fordert.AddChild(_forderung);
        _forderungLaufzeit = FmTheme.MakeLabel("", 13, FmTheme.TextSecondary);
        _forderungLaufzeit.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        fordert.AddChild(_forderungLaufzeit);
        spalten.AddChild(fordertKarte);

        var (bieteKarte, biete) = Karte("Dein Angebot");
        var gehaltZeile = new HBoxContainer();
        gehaltZeile.AddThemeConstantOverride("separation", 10);
        _gehalt = new SpinBox { MinValue = 0, MaxValue = 50_000_000, Step = 1, CustomMinimumSize = new Vector2(170, 0),
            Suffix = "€", Editable = false };
        _gehaltText = FmTheme.MakeLabel("", 13, FmTheme.TextSecondary);
        _gehalt.ValueChanged += wert => GehaltGeaendert(wert, ausRegler: false);
        gehaltZeile.AddChild(_gehalt);
        gehaltZeile.AddChild(_gehaltText);
        biete.AddChild(gehaltZeile);
        _regler = new HSlider { MinValue = 0, MaxValue = 1, Step = 1, Editable = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 22) };
        _regler.AddThemeStyleboxOverride("slider", FmTheme.Flaeche(FmTheme.BgPanel, 4, FmTheme.Border));
        _regler.AddThemeStyleboxOverride("grabber_area", FmTheme.Flaeche(FmTheme.Accent, 4));
        _regler.AddThemeStyleboxOverride("grabber_area_highlight", FmTheme.Flaeche(FmTheme.Accent, 4));
        _regler.TooltipText = "Bis ganz rechts: genau seine Forderung bei der gewählten Laufzeit.";
        _regler.ValueChanged += wert => GehaltGeaendert(wert, ausRegler: true);
        biete.AddChild(_regler);
        biete.AddChild(FmTheme.MakeLabel("LAUFZEIT", 10, FmTheme.TextSecondary));
        _laufzeitPlatz = new HBoxContainer();
        biete.AddChild(_laufzeitPlatz);
        _laufzeitText = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        biete.AddChild(_laufzeitText);
        spalten.AddChild(bieteKarte);

        // Gesprächsverlauf
        _verlaufScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 170),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var verlaufRahmen = new PanelContainer();
        var stil = FmTheme.Flaeche(FmTheme.BgDark, 10, FmTheme.Border);
        stil.SetContentMarginAll(10);
        verlaufRahmen.AddThemeStyleboxOverride("panel", stil);
        _verlauf = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _verlauf.AddThemeConstantOverride("separation", 6);
        _verlaufScroll.AddChild(_verlauf);
        verlaufRahmen.AddChild(_verlaufScroll);
        inhalt.AddChild(verlaufRahmen);

        _hinweis = FmTheme.MakeLabel(Wechsel
                ? "Bricht er ab oder stehst du nach einem Angebot auf, platzt der Wechsel - und er redet zwei Wochen lang nicht mehr mit uns."
                : "Bricht er ab oder stehst du nach einem Angebot auf, redet er zwei Wochen lang nicht mehr darüber.",
            12, FmTheme.TextGedimmt);
        _hinweis.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        inhalt.AddChild(_hinweis);

        var leiste = new HBoxContainer();
        leiste.AddThemeConstantOverride("separation", 10);
        _beenden = new Button { Text = "Gespräch beenden" };
        FmTheme.ApplySecondaryButton(_beenden);
        _beenden.Pressed += Schliessen;
        leiste.AddChild(_beenden);
        leiste.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _erfuellen = new Button { Text = "Forderung erfüllen", Disabled = true };
        FmTheme.ApplySecondaryButton(_erfuellen);
        _erfuellen.TooltipText = "Bietet genau seine Forderung bei der gewählten Laufzeit - er unterschreibt sofort.";
        _erfuellen.Pressed += async () => await ErfuelleForderung();
        leiste.AddChild(_erfuellen);
        _bieten = new Button { Text = "Angebot machen", Disabled = true };
        FmTheme.ApplyPrimaryButton(_bieten);
        _bieten.Pressed += async () => await Biete();
        leiste.AddChild(_bieten);
        inhalt.AddChild(leiste);

        _ = Beginne();
    }

    private static (Control Karte, VBoxContainer Box) Karte(string titel)
    {
        var karte = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var stil = FmTheme.Flaeche(FmTheme.BgDark, 10, FmTheme.Border);
        stil.SetContentMarginAll(14);
        karte.AddThemeStyleboxOverride("panel", stil);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        box.AddChild(FmTheme.MakeAbschnitt(titel, 17));
        karte.AddChild(box);
        return (karte, box);
    }

    // ── Ablauf ───────────────────────────────────────────────────────────────

    private async Task Beginne()
    {
        string start = _angebot != null ? $"transfer/angebot/{_angebot.Id}/verhandlung" : $"vertrag/{_spielerId}/verhandlung";
        var (stand, fehler) = await ApiClient.SendeAsync<Verhandlung>("POST", start);
        if (!IsInstanceValid(this)) return;
        if (stand == null)
        {
            SetzeUntertitel(_name);
            ZeigeFehler(fehler ?? "Das Gespräch kam nicht zustande.");
            _beenden.Text = "Schließen";
            return;
        }
        SetzeUntertitel(_angebot != null
            ? $"{stand.Name}  ·  {stand.Position}, {stand.Alter} Jahre  ·  Stärke {_angebot.StaerkeText}  ·  "
              + $"Ablöse {FmTheme.Geld(_angebot.Abloese)} an {_angebot.VonVerein}  ·  "
              + $"bisher {FmTheme.Gehalt(stand.AktuellesGehalt)} ({Gehaltsformat.PeriodeLabel})"
            : $"{stand.Name}  ·  {stand.Position}, {stand.Alter} Jahre  ·  Stärke {stand.Staerke}  ·  "
              + $"jetzt {FmTheme.Gehalt(stand.AktuellesGehalt)} ({Gehaltsformat.PeriodeLabel}), "
              + $"Vertrag bis 30.06.{stand.VertragBis + 1}");

        var texte = Enumerable.Range(stand.MinLaufzeit, stand.MaxLaufzeit - stand.MinLaufzeit + 1)
            .Select(j => j == 1 ? "1 Jahr" : $"{j} Jahre").ToArray();
        _laufzeit = Segmentschalter.Neu(texte, Math.Max(0, stand.Wunschlaufzeit - stand.MinLaufzeit),
            new Vector2(78, 30), 12);
        _laufzeit.Gewaehlt += _ => LaufzeitGeaendert();
        _laufzeitPlatz.AddChild(_laufzeit);

        _gehalt.Step = _regler.Step = Schritt;
        _gehalt.Suffix = $"€ / {Gehaltsformat.PeriodeLabel}";
        _stand = stand;
        SetzeReglerSpanne();
        _gehalt.Value = AusWoche(Math.Min(Math.Max(stand.AktuellesGehalt, stand.Marktsatz), ForderungBeiWahl));
        _gehalt.Editable = _regler.Editable = true;
        Uebernimm(stand);
    }

    private int GewaehlteLaufzeit => _stand == null || _laufzeit == null
        ? 0 : _stand.MinLaufzeit + _laufzeit.Auswahl;

    /// <summary>Seine Forderung bei der gewählten Laufzeit - dieselbe Zahl, die er im Gespräch nennt.</summary>
    private long ForderungBeiWahl => _stand?.ForderungBei(GewaehlteLaufzeit) ?? 0;

    private async Task Biete() => await Sende(ZuWoche(_gehalt.Value), GewaehlteLaufzeit);

    private async Task ErfuelleForderung()
    {
        if (_stand == null) return;
        long forderung = ForderungBeiWahl;
        _gehalt.Value = AusWoche(forderung);
        await Sende(forderung, GewaehlteLaufzeit);
    }

    private async Task Sende(long wochengehalt, int laufzeit)
    {
        if (_stand == null || !_stand.Laeuft) return;
        _bieten.Disabled = _erfuellen.Disabled = true;
        var (stand, fehler) = await ApiClient.SendeAsync<Verhandlung>("POST",
            $"vertrag/{_spielerId}/verhandlung/angebot", new { gehalt = wochengehalt, laufzeit });
        if (!IsInstanceValid(this)) return;
        if (stand == null)
        {
            ZeigeFehler(fehler ?? "Das Angebot kam nicht an.");
            _bieten.Disabled = _erfuellen.Disabled = !(_stand?.Laeuft ?? false);
            return;
        }
        Uebernimm(stand);
    }

    private void Uebernimm(Verhandlung stand)
    {
        _stand = stand;
        ZeigeStimmung(stand.Stimmung);
        AktualisiereForderung();
        SetzeReglerSpanne();

        foreach (var kind in _verlauf.GetChildren()) kind.QueueFree();
        foreach (var zeile in stand.Verlauf) _verlauf.AddChild(Sprechblase(zeile));
        _ = ScrolleNachUnten();

        AktualisiereLaufzeitText();
        AktualisiereGehaltText();
        if (!stand.Laeuft) Beende(stand);
        else _bieten.Disabled = _erfuellen.Disabled = false;
    }

    private void Beende(Verhandlung stand)
    {
        _gehalt.Editable = _regler.Editable = false;
        _bieten.Disabled = _erfuellen.Disabled = true;
        _beenden.Text = "Schließen";
        string vertrag = $"{FmTheme.Gehalt(stand.NeuesGehalt ?? 0)} je {Gehaltsformat.PeriodeLabel}, "
                         + $"bis 30.06.{(stand.NeuerVertragBis ?? 0) + 1}";
        switch (stand.Status)
        {
            case "EINIG":
                _hinweis.Text = Wechsel ? $"Einigung - {stand.Name} unterschreibt: {vertrag}." : $"Vertrag verlängert: {vertrag}.";
                _hinweis.AddThemeColorOverride("font_color", FmTheme.Success);
                if (_spieler != null)
                {
                    _spieler.Gehalt = stand.NeuesGehalt ?? _spieler.Gehalt;
                    _spieler.VertragBis = stand.NeuerVertragBis ?? _spieler.VertragBis;
                }
                break;
            default:
                _hinweis.Text = Wechsel ? "Die Stimmung ist gekippt - er hat abgebrochen, der Wechsel ist geplatzt."
                    : "Die Stimmung ist gekippt - er hat abgebrochen. In zwei Wochen kannst du es wieder versuchen.";
                _hinweis.AddThemeColorOverride("font_color", FmTheme.Danger);
                break;
        }
        // Bei einem Neuzugang ändert auch ein Scheitern etwas - das Angebot ist dann vom Tisch.
        if (stand.Status == "EINIG" || Wechsel) _geaendert?.Invoke();
    }

    protected override void BeimSchliessen()
    {
        // Ein laufendes Gespräch ist mit dem Aufstehen vorbei.
        if (_stand?.Laeuft == true)
            _ = ApiClient.SendeAsync("DELETE", $"vertrag/{_spielerId}/verhandlung");
    }

    // ── Anzeige ──────────────────────────────────────────────────────────────

    private void ZeigeStimmung(int stimmung)
    {
        var farbe = stimmung >= 60 ? FmTheme.Success : stimmung >= 30 ? FmTheme.Gold : FmTheme.Danger;
        _stimmungZahl.Text = stimmung.ToString();
        _stimmungZahl.AddThemeColorOverride("font_color", farbe);
        _stimmung.Value = stimmung;
        _stimmung.AddThemeStyleboxOverride("fill", FmTheme.Flaeche(farbe, 6));
        _stimmungWort.Text = "STIMMUNG  ·  " + (stimmung >= 80 ? "gut gelaunt" : stimmung >= 60 ? "gesprächsbereit"
            : stimmung >= 40 ? "angespannt" : stimmung >= 20 ? "gereizt" : stimmung > 0 ? "kurz vor dem Abbruch" : "abgebrochen");
    }

    private void AktualisiereForderung()
    {
        if (_stand == null) return;
        int jahre = GewaehlteLaufzeit;
        _forderung.Text = FmTheme.Gehalt(ForderungBeiWahl);
        string laufzeit = jahre == 1 ? "1 Jahr" : $"{jahre} Jahre";
        string wunsch = jahre == _stand.Wunschlaufzeit
            ? "seine Wunschlaufzeit"
            : $"Wunsch: {(_stand.Wunschlaufzeit == 1 ? "1 Jahr" : $"{_stand.Wunschlaufzeit} Jahre")} für {FmTheme.Gehalt(_stand.Forderung)}";
        _forderungLaufzeit.Text = $"je {Gehaltsformat.PeriodeLabel}, bei {laufzeit} (bis 30.06.{_stand.EndetIm(jahre)})  ·  "
                                  + $"{wunsch}  ·  üblich wären {FmTheme.Gehalt(_stand.Marktsatz)}";
    }

    /// <summary>
    /// Der Regler reicht bis zu seiner Forderung bei der gewählten Laufzeit; links bis gut die
    /// Hälfte davon - weiter unten ist jedes Angebot eine Beleidigung.
    /// </summary>
    private void SetzeReglerSpanne()
    {
        if (_stand == null) return;
        double bis = AusWoche(ForderungBeiWahl);
        double von = Math.Floor(bis * 0.55 / Schritt) * Schritt;
        _gleichtAb = true;
        _regler.MinValue = von;
        _regler.MaxValue = bis;
        _regler.Value = Math.Clamp(_gehalt.Value, von, bis);
        _gleichtAb = false;
    }

    private void GehaltGeaendert(double wert, bool ausRegler)
    {
        if (_gleichtAb) return;
        _gleichtAb = true;
        if (ausRegler) _gehalt.Value = wert;
        else _regler.Value = Math.Clamp(wert, _regler.MinValue, _regler.MaxValue);
        _gleichtAb = false;
        AktualisiereGehaltText();
    }

    private void LaufzeitGeaendert()
    {
        AktualisiereForderung();
        SetzeReglerSpanne();
        AktualisiereLaufzeitText();
        AktualisiereGehaltText();
    }

    private void AktualisiereGehaltText()
    {
        if (_stand == null) return;
        long woche = ZuWoche(_gehalt.Value);
        long forderung = ForderungBeiWahl;
        string abstand = woche >= forderung ? "erfüllt seine Forderung"
            : $"{(forderung - woche) * 100.0 / forderung:0.0} % unter seiner Forderung";
        string gegenJetzt = _stand.AktuellesGehalt > 0
            ? $"  ·  {(woche >= _stand.AktuellesGehalt ? "+" : "−")}{Math.Abs((woche - _stand.AktuellesGehalt) * 100.0 / _stand.AktuellesGehalt):0} % gegenüber {(Wechsel ? "bisher" : "jetzt")}"
            : "";
        _gehaltText.Text = abstand + gegenJetzt;
    }

    private void AktualisiereLaufzeitText()
    {
        if (_stand == null || _laufzeit == null) return;
        int jahre = GewaehlteLaufzeit;
        string wunsch = jahre == _stand.Wunschlaufzeit ? "  ·  wie er es sich wünscht"
            : jahre > _stand.Wunschlaufzeit ? "  ·  länger, als er sich binden will - kostet Aufschlag"
            : "  ·  kürzer, als er sich wünscht - kostet Aufschlag";
        _laufzeitText.Text = $"bis 30.06.{_stand.EndetIm(jahre)}{wunsch}";
    }

    /// <summary>"92.560 € im Jahr" - in der Periode, die der Nutzer eingestellt hat.</summary>
    private static string MitPeriode(long wochengehalt) => FmTheme.Gehalt(wochengehalt) + Gehaltsformat.Periode switch
    {
        GehaltsPeriode.Monat => " im Monat",
        GehaltsPeriode.Jahr  => " im Jahr",
        _                    => " in der Woche",
    };

    private static Control Sprechblase(VerhandlungsZeile zeile)
    {
        bool verein = zeile.Wer == "VEREIN";
        var reihe = new HBoxContainer();
        if (verein) reihe.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var blase = new PanelContainer { CustomMinimumSize = new Vector2(0, 0) };
        var stil = FmTheme.Flaeche(verein ? FmTheme.BgPanel.Lerp(FmTheme.Accent, 0.15f) : FmTheme.BgKnopf, 10);
        stil.ContentMarginLeft = stil.ContentMarginRight = 12;
        stil.ContentMarginTop = stil.ContentMarginBottom = 6;
        blase.AddThemeStyleboxOverride("panel", stil);
        string satz = zeile.Betrag is { } betrag ? zeile.Text.Replace("{betrag}", MitPeriode(betrag)) : zeile.Text;
        var text = FmTheme.MakeLabel((verein ? "Du: " : "") + satz, 13, FmTheme.TextPrimary);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = new Vector2(520, 0);
        blase.AddChild(text);
        reihe.AddChild(blase);
        if (!verein) reihe.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        return reihe;
    }

    private async Task ScrolleNachUnten()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (IsInstanceValid(this))
            _verlaufScroll.ScrollVertical = (int)_verlaufScroll.GetVScrollBar().MaxValue;
    }

    /// <summary>Für den Vorschaumodus: bietet einen Anteil seiner Forderung.</summary>
    internal async Task VorschauBiete(double anteil)
    {
        for (int i = 0; i < 40 && _stand == null; i++) await ToSignal(GetTree().CreateTimer(0.1), "timeout");
        if (_stand == null) return;
        _gehalt.Value = AusWoche((long)(ForderungBeiWahl * anteil));
        await Biete();
    }
}
