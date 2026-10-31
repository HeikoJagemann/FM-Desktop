#nullable enable
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Vertragsverhandlung mit einem eigenen Spieler - gegen die Uhr.
///
/// <para>Er nennt Gehalt und Laufzeit, du bietest. Er kommt dir entgegen, aber nie unter das, was
/// er sich wert ist; zu niedrige Angebote verstimmen ihn. Nach sechzig Sekunden ist das Gespräch
/// vorbei. Die Uhr läuft im Server - die Anzeige hier zählt nur mit.</para>
///
/// <para>Gehälter erscheinen in der eingestellten Periode (Woche, Monat oder Jahr); gerechnet
/// wird im Server immer mit dem Wochengehalt.</para>
/// </summary>
public partial class VertragsverhandlungDialog : Overlaykarte
{
    private Spieler _spieler = null!;
    private Action? _geaendert;
    private Verhandlung? _stand;
    private double _rest;
    private bool _fragtNach;

    private Label _uhr = null!;
    private ProgressBar _uhrBalken = null!;
    private Label _forderung = null!;
    private Label _forderungLaufzeit = null!;
    private ProgressBar _stimmung = null!;
    private SpinBox _gehalt = null!;
    private Label _gehaltText = null!;
    private Segmentschalter? _laufzeit;
    private HBoxContainer _laufzeitPlatz = null!;
    private Label _laufzeitText = null!;
    private VBoxContainer _verlauf = null!;
    private ScrollContainer _verlaufScroll = null!;
    private Button _bieten = null!;
    private Button _annehmen = null!;
    private Button _beenden = null!;
    private Label _hinweis = null!;

    public VertragsverhandlungDialog() : base("Vertragsverhandlung", 900)
    {
        SchliesstBeiKlickDaneben = false;
    }

    /// <param name="geaendert">wenn ein neuer Vertrag zustande kam</param>
    public static void Zeige(Node caller, Spieler spieler, Action? geaendert = null)
    {
        Oeffne(caller, new VertragsverhandlungDialog { _spieler = spieler, _geaendert = geaendert },
            spieler.Id.ToString());
    }

    // ── Gehalt in der eingestellten Periode ──────────────────────────────────

    private static double Faktor => Gehaltsformat.Periode switch
    {
        GehaltsPeriode.Monat => 52.0 / 12.0,
        GehaltsPeriode.Jahr  => 52.0,
        _                    => 1.0,
    };

    private static long ZuWoche(double angezeigt) => (long)Math.Round(angezeigt / Faktor);
    private static double AusWoche(long woche) => Math.Round(woche * Faktor);

    // ── Aufbau ───────────────────────────────────────────────────────────────

    protected override void BaueInhalt(VBoxContainer inhalt)
    {
        SetzeUntertitel($"{_spieler.Name}  ·  Gespräch wird vorbereitet …");

        // Die Uhr
        var uhrZeile = new HBoxContainer();
        uhrZeile.AddThemeConstantOverride("separation", 14);
        _uhr = FmTheme.MakeZahl("60", 40, FmTheme.Accent);
        _uhr.CustomMinimumSize = new Vector2(70, 0);
        uhrZeile.AddChild(_uhr);
        _uhrBalken = new ProgressBar
        {
            MinValue = 0, MaxValue = 60, Value = 60, ShowPercentage = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(0, 12),
        };
        _uhrBalken.AddThemeStyleboxOverride("background", FmTheme.Flaeche(FmTheme.BgDark, 6, FmTheme.Border));
        _uhrBalken.AddThemeStyleboxOverride("fill", FmTheme.Flaeche(FmTheme.Accent, 6));
        uhrZeile.AddChild(_uhrBalken);
        inhalt.AddChild(uhrZeile);

        // Forderung links, Angebot rechts
        var spalten = new HBoxContainer();
        spalten.AddThemeConstantOverride("separation", 16);
        inhalt.AddChild(spalten);

        var (fordertKarte, fordert) = Karte("Er fordert");
        _forderung = FmTheme.MakeZahl("–", 30, FmTheme.TextPrimary);
        fordert.AddChild(_forderung);
        _forderungLaufzeit = FmTheme.MakeLabel("", 13, FmTheme.TextSecondary);
        fordert.AddChild(_forderungLaufzeit);
        fordert.AddChild(FmTheme.MakeLabel("STIMMUNG", 10, FmTheme.TextSecondary));
        _stimmung = new ProgressBar { MinValue = 0, MaxValue = 100, Value = 70, ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 8) };
        _stimmung.AddThemeStyleboxOverride("background", FmTheme.Flaeche(FmTheme.BgDark, 4, FmTheme.Border));
        _stimmung.AddThemeStyleboxOverride("fill", FmTheme.Flaeche(FmTheme.Success, 4));
        _stimmung.TooltipText = "Wie das Gespräch läuft. Angebote weit unter seinem Wert verstimmen ihn - zweimal, und er geht.";
        fordert.AddChild(_stimmung);
        spalten.AddChild(fordertKarte);

        var (bieteKarte, biete) = Karte("Dein Angebot");
        var gehaltZeile = new HBoxContainer();
        gehaltZeile.AddThemeConstantOverride("separation", 10);
        _gehalt = new SpinBox { MinValue = 0, MaxValue = 50_000_000, Step = 10, CustomMinimumSize = new Vector2(170, 0),
            Suffix = "€", Editable = false };
        _gehaltText = FmTheme.MakeLabel("", 13, FmTheme.TextSecondary);
        _gehalt.ValueChanged += _ => AktualisiereGehaltText();
        gehaltZeile.AddChild(_gehalt);
        gehaltZeile.AddChild(_gehaltText);
        biete.AddChild(gehaltZeile);
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

        _hinweis = FmTheme.MakeLabel("Brichst du nach einem Angebot ab oder läuft die Zeit ab, redet er zwei Wochen lang nicht mehr darüber.",
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
        _annehmen = new Button { Text = "Forderung annehmen", Disabled = true };
        FmTheme.ApplySecondaryButton(_annehmen);
        _annehmen.TooltipText = "Seine Forderung bei seiner Wunschlaufzeit - er unterschreibt sofort.";
        _annehmen.Pressed += async () => await NimmForderungAn();
        leiste.AddChild(_annehmen);
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
        var (stand, fehler) = await ApiClient.SendeAsync<Verhandlung>("POST", $"vertrag/{_spieler.Id}/verhandlung");
        if (!IsInstanceValid(this)) return;
        if (stand == null)
        {
            SetzeUntertitel(_spieler.Name);
            ZeigeFehler(fehler ?? "Das Gespräch kam nicht zustande.");
            _uhr.Text = "–";
            _beenden.Text = "Schließen";
            return;
        }
        SetzeUntertitel($"{stand.Name}  ·  {stand.Position}, {stand.Alter} Jahre  ·  Stärke {stand.Staerke}  ·  "
                        + $"jetzt {FmTheme.Gehalt(stand.AktuellesGehalt)} ({Gehaltsformat.PeriodeLabel}), "
                        + $"Vertrag bis 30.06.{stand.VertragBis + 1}");

        var texte = Enumerable.Range(stand.MinLaufzeit, stand.MaxLaufzeit - stand.MinLaufzeit + 1)
            .Select(j => j == 1 ? "1 Jahr" : $"{j} Jahre").ToArray();
        _laufzeit = Segmentschalter.Neu(texte, Math.Max(0, stand.Wunschlaufzeit - stand.MinLaufzeit),
            new Vector2(78, 30), 12);
        _laufzeit.Gewaehlt += _ => AktualisiereLaufzeitText();
        _laufzeitPlatz.AddChild(_laufzeit);

        _gehalt.Step = Faktor >= 52 ? 1_000 : Faktor > 1 ? 100 : 10;
        _gehalt.Suffix = $"€ / {Gehaltsformat.PeriodeLabel}";
        _gehalt.Value = AusWoche(Math.Max(stand.AktuellesGehalt, stand.Marktsatz));
        _gehalt.Editable = true;
        _bieten.Disabled = false;
        _annehmen.Disabled = false;
        Uebernimm(stand);
    }

    private int GewaehlteLaufzeit => _stand == null || _laufzeit == null
        ? 0 : _stand.MinLaufzeit + _laufzeit.Auswahl;

    private async Task Biete() => await Sende(ZuWoche(_gehalt.Value), GewaehlteLaufzeit);

    private async Task NimmForderungAn()
    {
        if (_stand == null || _laufzeit == null) return;
        _laufzeit.Auswahl = _stand.Wunschlaufzeit - _stand.MinLaufzeit;
        _gehalt.Value = AusWoche(_stand.Forderung);
        await Sende(_stand.Forderung, _stand.Wunschlaufzeit);
    }

    private async Task Sende(long wochengehalt, int laufzeit)
    {
        if (_stand == null || !_stand.Laeuft) return;
        _bieten.Disabled = _annehmen.Disabled = true;
        var (stand, fehler) = await ApiClient.SendeAsync<Verhandlung>("POST",
            $"vertrag/{_spieler.Id}/verhandlung/angebot", new { gehalt = wochengehalt, laufzeit });
        if (!IsInstanceValid(this)) return;
        if (stand == null)
        {
            ZeigeFehler(fehler ?? "Das Angebot kam nicht an.");
            _bieten.Disabled = _annehmen.Disabled = !(_stand?.Laeuft ?? false);
            return;
        }
        Uebernimm(stand);
    }

    /// <summary>Die Uhr ist abgelaufen - der Server soll das Gespräch beenden, damit es zählt.</summary>
    private async Task FrageNachEnde()
    {
        _fragtNach = true;
        var (stand, _) = await ApiClient.SendeAsync<Verhandlung>("GET", $"vertrag/{_spieler.Id}/verhandlung");
        if (!IsInstanceValid(this)) return;
        _fragtNach = false;
        if (stand != null) Uebernimm(stand);
    }

    private void Uebernimm(Verhandlung stand)
    {
        _stand = stand;
        _rest = stand.RestSekunden;
        _forderung.Text = $"{FmTheme.Gehalt(stand.Forderung)}";
        _forderungLaufzeit.Text = $"je {Gehaltsformat.PeriodeLabel}, über {(stand.Wunschlaufzeit == 1 ? "1 Jahr" : $"{stand.Wunschlaufzeit} Jahre")} "
                                  + $"(bis 30.06.{stand.EndetIm(stand.Wunschlaufzeit)})  ·  üblich wären {FmTheme.Gehalt(stand.Marktsatz)}";
        _stimmung.Value = stand.Stimmung;
        _stimmung.AddThemeStyleboxOverride("fill", FmTheme.Flaeche(
            stand.Stimmung >= 50 ? FmTheme.Success : stand.Stimmung >= 25 ? FmTheme.Gold : FmTheme.Danger, 4));

        foreach (var kind in _verlauf.GetChildren()) kind.QueueFree();
        foreach (var zeile in stand.Verlauf) _verlauf.AddChild(Sprechblase(zeile));
        _ = ScrolleNachUnten();

        AktualisiereLaufzeitText();
        AktualisiereGehaltText();
        if (!stand.Laeuft) Beende(stand);
        else _bieten.Disabled = _annehmen.Disabled = false;
    }

    private void Beende(Verhandlung stand)
    {
        _gehalt.Editable = false;
        _bieten.Disabled = _annehmen.Disabled = true;
        _rest = 0;
        ZeigeUhr();
        _beenden.Text = "Schließen";
        switch (stand.Status)
        {
            case "EINIG":
                _hinweis.Text = $"Vertrag verlängert: {FmTheme.Gehalt(stand.NeuesGehalt ?? 0)} je {Gehaltsformat.PeriodeLabel}, "
                                + $"bis 30.06.{(stand.NeuerVertragBis ?? 0) + 1}.";
                _hinweis.AddThemeColorOverride("font_color", FmTheme.Success);
                _spieler.Gehalt = stand.NeuesGehalt ?? _spieler.Gehalt;
                _spieler.VertragBis = stand.NeuerVertragBis ?? _spieler.VertragBis;
                _geaendert?.Invoke();
                break;
            case "ABGELAUFEN":
                _hinweis.Text = "Die Zeit ist abgelaufen - keine Einigung. In zwei Wochen kannst du es wieder versuchen.";
                _hinweis.AddThemeColorOverride("font_color", FmTheme.Danger);
                break;
            default:
                _hinweis.Text = "Er hat das Gespräch abgebrochen. In zwei Wochen kannst du es wieder versuchen.";
                _hinweis.AddThemeColorOverride("font_color", FmTheme.Danger);
                break;
        }
    }

    protected override void BeimSchliessen()
    {
        // Ein laufendes Gespräch ist mit dem Aufstehen vorbei.
        if (_stand?.Laeuft == true)
            _ = ApiClient.SendeAsync("DELETE", $"vertrag/{_spieler.Id}/verhandlung");
    }

    public override void _Process(double delta)
    {
        if (_stand == null || !_stand.Laeuft) return;
        _rest = Math.Max(0, _rest - delta);
        ZeigeUhr();
        if (_rest <= 0 && !_fragtNach) _ = FrageNachEnde();
    }

    private void ZeigeUhr()
    {
        _uhr.Text = $"{Math.Ceiling(_rest):0}";
        _uhrBalken.Value = _rest;
        var farbe = _rest <= 10 ? FmTheme.Danger : _rest <= 25 ? FmTheme.Gold : FmTheme.Accent;
        _uhr.AddThemeColorOverride("font_color", farbe);
        _uhrBalken.AddThemeStyleboxOverride("fill", FmTheme.Flaeche(farbe, 6));
    }

    // ── Anzeige ──────────────────────────────────────────────────────────────

    private void AktualisiereGehaltText()
    {
        if (_stand == null) return;
        long woche = ZuWoche(_gehalt.Value);
        double gegenJetzt = _stand.AktuellesGehalt > 0 ? (woche - _stand.AktuellesGehalt) * 100.0 / _stand.AktuellesGehalt : 0;
        _gehaltText.Text = FmTheme.Gehalt(woche) + (_stand.AktuellesGehalt > 0
            ? $"  ·  {(gegenJetzt >= 0 ? "+" : "−")}{Math.Abs(gegenJetzt):0} % gegenüber jetzt"
            : "");
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
        _gehalt.Value = AusWoche((long)(_stand.Forderung * anteil));
        await Biete();
    }
}
