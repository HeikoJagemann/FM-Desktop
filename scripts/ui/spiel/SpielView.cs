#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Die eigene Partie als eigener Bildschirm - vom Anpfiff bis zur Zusammenfassung.
///
/// <para>Löst den früheren Popup-Dialog ab. Ein Spiel ist der Höhepunkt einer Woche und bekommt
/// deshalb den ganzen Bildschirm: Die Toolbar des Hauptmenüs wäre hier ohnehin irreführend, weil
/// während einer laufenden Partie weder navigiert noch die Zeit weitergeschaltet werden darf.</para>
///
/// <para>Der Bildschirm hat zwei Zustände: die laufende Partie und danach die Zusammenfassung.
/// Beide leben in derselben Szene, damit der Übergang ohne Ladepause geschieht.</para>
/// </summary>
public partial class SpielView : Control
{
    private const int Spielminuten = 90;
    private const int Halbzeitminuten = Spielminuten / 2;

    private const string SceneGameMain = "res://scenes/GameMain.tscn";

    private LiveSpiel? _spiel;

    /// <summary>
    /// Ob die Uhr steht - waehrend eines Wechsels und nach dem Abpfiff.
    ///
    /// <para>Es gibt nur noch eine Uhr, und die kommt vom Spielfeld: Wie weit die Partie ist,
    /// ergibt sich daraus, was man gerade sieht. Vorher lief daneben ein Timer, der alle zwei
    /// Zehntelsekunden eine Spielminute weiterschaltete - die Partie war damit nach achtzehn
    /// Sekunden vorbei, während auf dem Platz noch niemand am Ball war.</para>
    /// </summary>
    private bool _pausiert;

    // Kopf
    private Label _ergebnisLabel = null!;
    private Label _minuteLabel = null!;
    private Label _heimNameLabel = null!;
    private Label _gastNameLabel = null!;
    private ProgressBar _uhr = null!;

    // Rumpf
    private Control _rumpf = null!;
    private Label _halbzeitLabel = null!;
    private Label _richtungLabel = null!;
    /// <summary>Die Tempo-Knöpfe, je Faktor - der gewählte steht in Bernstein.</summary>
    private readonly System.Collections.Generic.Dictionary<double, Button> _tempoKnoepfe = new();
    private Panel _pausenschild = null!;
    private Label _pausenText = null!;
    private VBoxContainer _heimSpalte = null!;
    private VBoxContainer _gastSpalte = null!;
    private Label _heimStaerkeLabel = null!;
    private Label _gastStaerkeLabel = null!;
    private VBoxContainer _log = null!;
    private ScrollContainer _logScroll = null!;
    private StatistikTafel _statistik = null!;

    // Spielfeldansicht der Agenten-Engine - eigener Abruf, eigene Uhr.
    private Spielfeld _spielfeld = null!;
    private Label _spielfeldHinweis = null!;
    private readonly SzenenPlayer _szenen = new();
    private bool _spielfeldLaeuft;
    private bool _spielfeldAnfrage;
    private double _spielfeldTempo = 1.0;

    /// <summary>Halbzeit der Agenten-Partie - 1 oder 2. Steuert Uhr und Anzeige.</summary>
    private int _halbzeit = 1;

    /// <summary>Torstand der Agenten-Partie; das ist der Stand, der auf dem Platz zu sehen ist.</summary>
    private int _feldHeimTore;
    private int _feldGastTore;

    /// <summary>Bis zu welcher Minute die Partie schon gerechnet ist.</summary>
    private int _gerechneteMinute;

    /// <summary>Wie viele Abrufe hintereinander nichts geliefert haben.</summary>
    private int _fehlschlaege;

    /// <summary>Woran es gerade liegt, wenn das Bild steht - fuer die Kopfzeile.</summary>
    private string _spielfeldLage = "";

    // Fußleiste
    private Label _wechselLabel = null!;
    private Button _wechselButton = null!;
    private Button _aktionButton = null!;

    private bool _abgepfiffen;
    private bool _wartetAufServer;
    private bool _spieltagLaeuft;

    /// <summary>
    /// Ob dieser Bildschirm noch steht.
    ///
    /// <para>Nach jedem <c>await</c> zu prüfen, bevor Knoten angefasst werden: Während eine
    /// Anfrage unterwegs ist, kann der Nutzer längst zurückgegangen sein oder das Fenster
    /// geschlossen haben. Dann ist dieser Knoten freigegeben und jeder Zugriff darauf - auch
    /// <c>GetTree()</c> - läuft in eine NullReferenceException.</para>
    /// </summary>
    private bool Lebt => IsInstanceValid(this) && !IsQueuedForDeletion();

    public override async void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BaueUI();

        var (start, status) = await ApiClient.PostMitStatus<object, SpielfeldPaket>(
            $"spielfeld/start?vereinId={GameState.Instance.VereinId}", new { });

        if (!Lebt) return;

        if (start == null)
        {
            // 204 heißt: Es ist schlicht kein Spiel angesetzt. Das ist kein Fehler, sondern der
            // Normalfall an jedem Tag ohne Partie - und muss anders aussehen als ein Aussetzer.
            ZeigeFehler(status == 204
                ? "Heute ist kein Spiel angesetzt.\nSpiele im Kalender weiter bis zum naechsten Spieltag."
                : status == 0
                    ? "Keine Verbindung zum Server.\nLaeuft das Backend auf Port 8081?"
                    : $"Die Partie konnte nicht gestartet werden (HTTP {status}).");
            return;
        }

        if (start.Aufstellung != null)
        {
            _spielfeld.SetzeAufstellung(start.Aufstellung);
        }
        NimmPaketAuf(start);
        _spielfeldLaeuft = true;

        // Der Stand fuellt die Seitenspalten - Namen, Kondition, Wechselkonto.
        var stand = await ApiClient.GetAsync<LiveSpiel>("spielfeld/stand");
        if (!Lebt) return;
        if (stand == null)
        {
            ZeigeFehler("Die Partie konnte nicht gelesen werden.");
            return;
        }

        BenenneMannschaften(stand);
        SchreibeZeile(0, "Anpfiff", FmTheme.TextSecondary);
        Aktualisiere(stand);
    }

    // ── Spielfeldansicht ─────────────────────────────────────────────────────

    /// <summary>
    /// Wie viel Spielzeit im Puffer liegen soll, bevor nachgefordert wird - abhängig vom Tempo.
    ///
    /// <para>Bei zwanzigfachem Tempo verbraucht das Bild je Wanduhrsekunde zwanzig
    /// Spielsekunden. Ein fester Vorrat von zwei Sekunden wäre nach einem Zehntel aufgebraucht,
    /// und genau das ließ das Bild bei hohem Tempo stocken.</para>
    /// </summary>
    private double VorratMindestens => Math.Max(2.0, _spielfeldTempo * 1.5);

    /// <summary>Wie viel ein Abruf holt - deutlich mehr als der Vorrat, damit er nie leerläuft.</summary>
    private double AbrufSekunden => Math.Max(6.0, _spielfeldTempo * 4.0);

    /// <summary>
    /// Übernimmt aus einem Paket alles, was nicht gezeichnet wird: Torstand, Halbzeit, Verlauf.
    ///
    /// <para>Der Spielstand im Kopf ist der der Agenten-Partie - der, den man auf dem Platz
    /// fallen sieht. Vorher stand dort das Ergebnis der alten Engine, während auf dem Rasen
    /// etwas ganz anderes geschah; wer ein Tor sah, fand es nirgends wieder.</para>
    /// </summary>
    private void NimmPaketAuf(SpielfeldPaket paket)
    {
        _szenen.Fuettere(paket);
        _halbzeit = paket.Halbzeit;
        ZeigeRichtung(paket.HeimNachRechts);
        _feldHeimTore = paket.HeimTore;
        _feldGastTore = paket.GastTore;
        _ergebnisLabel.Text = $"{paket.HeimTore} : {paket.GastTore}";

        foreach (var v in paket.Vorfaelle)
        {
            SchreibeVorfall(v);
        }
    }

    /// <summary>Auf welches Tor die beiden Mannschaften gerade spielen.</summary>
    private void ZeigeRichtung(bool heimNachRechts)
    {
        string heim = _spiel?.HeimVerein ?? "Heim";
        string gast = _spiel?.GastVerein ?? "Gast";
        _richtungLabel.Text = heimNachRechts
            ? $"{heim} ▶   ◀ {gast}"
            : $"◀ {heim}   {gast} ▶";
    }

    /// <summary>Eine Zeile des Spielverlaufs, wie die Engine sie gemeldet hat.</summary>
    private void SchreibeVorfall(SpielfeldVorfall v)
    {
        bool eigenes = v.Heim == (_spiel?.EigenesHeimspiel ?? true);
        switch (v.Art)
        {
            case "TOR":
                SchreibeZeile(v.Minute, string.IsNullOrEmpty(v.Vorlage)
                        ? $"⚽  Tor - {v.Name}"
                        : $"⚽  Tor - {v.Name}  (Vorlage {v.Vorlage})",
                    eigenes ? FmTheme.Success : FmTheme.Danger);
                break;
            case "EIGENTOR":
                SchreibeZeile(v.Minute, $"⚽  Eigentor - {v.Name}",
                    eigenes ? FmTheme.Success : FmTheme.Danger);
                break;
            case "GELB":
                SchreibeZeile(v.Minute, $"🟨  {v.Name}", FmTheme.Gold);
                break;
            case "GELBROT":
                SchreibeZeile(v.Minute, $"🟨🟥  Gelb-Rot - {v.Name}", FmTheme.Danger);
                break;
            case "ROT":
                SchreibeZeile(v.Minute, $"🟥  Rote Karte - {v.Name}", FmTheme.Danger);
                break;
            case "HALBZEIT":
                SchreibeZeile(v.Minute,
                    $"⏸  Halbzeit - {_feldHeimTore} : {_feldGastTore}, Seitenwechsel",
                    FmTheme.TextPrimary);
                break;
            case "ABPFIFF":
                SchreibeZeile(v.Minute,
                    $"⏹  Abpfiff - {_feldHeimTore} : {_feldGastTore}", FmTheme.TextPrimary);
                break;
        }
    }

    /// <summary>
    /// Treibt die Wiedergabe und holt Nachschub, bevor der Puffer leerlaeuft.
    ///
    /// <para>Immer nur eine Anfrage gleichzeitig: Zwei ueberlappende Abrufe brachten Takte
    /// durcheinander, und der Puffer verwirft ohnehin, was er schon hat.</para>
    /// </summary>
    public override void _Process(double delta)
    {
        if (!_spielfeldLaeuft) return;

        if (_pausiert) return;

        var bild = _szenen.Ruecke(delta, _spielfeldTempo);
        if (bild != null)
        {
            _spielfeld.ZeigeBild(bild);
            ZeigeUhr(bild);
            TreibeDiePartie(Spielminute(bild.Anzeigezeit));
        }

        if (!_spielfeldAnfrage && _szenen.Vorrat < VorratMindestens)
        {
            _spielfeldAnfrage = true;
            _ = HoleNachschub();
        }

        ZeigeLage(bild);
    }

    /// <summary>
    /// Schreibt Spielzeit, Puffer und Tempo in die Kopfzeile.
    ///
    /// <para>Steht das Bild, soll dort stehen, woran es liegt. Vorher war ein Stillstand nicht
    /// von einem Absturz zu unterscheiden - man sah zweiundzwanzig Punkte, die sich nicht mehr
    /// bewegten, und sonst nichts.</para>
    /// </summary>
    private void ZeigeLage(SzenenPlayer.Bild? bild)
    {
        _zeitSeitAnzeige += 1;
        if (_zeitSeitAnzeige < 15) return;
        _zeitSeitAnzeige = 0;

        double sekunden = bild?.Anzeigezeit ?? 0;
        string uhr = $"{(int)(sekunden / 60):00}:{(int)(sekunden % 60):00}";
        string puffer = _szenen.Vorrat < 0.3 && _spielfeldAnfrage ? " · puffert …" : "";

        _spielfeldHinweis.Text = string.IsNullOrEmpty(_spielfeldLage)
            ? $"Agenten-Engine · {uhr} · Puffer {_szenen.Vorrat:0.0} s{puffer}"
            : $"Agenten-Engine · {uhr} · {_spielfeldLage}";
    }

    private int _zeitSeitAnzeige;

    /// <summary>Nach so vielen erfolglosen Abrufen hintereinander wird aufgegeben.</summary>
    private const int FehlschlaegeBisAufgabe = 5;

    /// <summary>
    /// Holt das naechste Paket.
    ///
    /// <para>Ein einzelner Fehlschlag beendet die Ansicht nicht mehr. Vorher genuegte eine
    /// Zeitueberschreitung oder ein kurzer Aussetzer des Servers, und das Bild stand fuer immer
    /// still - ohne Meldung, ununterscheidbar von einem Absturz. Jetzt wird es erneut versucht
    /// und die Lage steht in der Kopfzeile.</para>
    /// </summary>
    private async Task HoleNachschub()
    {
        var paket = await ApiClient.PostAsync<object, SpielfeldPaket>(
            $"spielfeld/weiter?sekunden={AbrufSekunden.ToString(CultureInfo.InvariantCulture)}",
            new { });

        if (!Lebt) return;
        _spielfeldAnfrage = false;

        if (paket == null)
        {
            _fehlschlaege++;
            if (_fehlschlaege >= FehlschlaegeBisAufgabe)
            {
                _spielfeldLaeuft = false;
                _spielfeldLage = "Server antwortet nicht mehr - Ansicht angehalten.";
                _spielfeldHinweis.AddThemeColorOverride("font_color", FmTheme.Danger);
                _zeitSeitAnzeige = 99;
                ZeigeLage(null);
                return;
            }
            _spielfeldLage = $"keine Antwort, Versuch {_fehlschlaege} von {FehlschlaegeBisAufgabe} …";
            return;
        }

        _fehlschlaege = 0;
        _spielfeldLage = "";
        NimmPaketAuf(paket);

        if (paket.Beendet && _szenen.Vorrat <= 0)
        {
            _spielfeldLaeuft = false;
            _spielfeldHinweis.Text = $"Abpfiff - {paket.HeimTore}:{paket.GastTore}";
        }
    }

    // ── Aufbau ───────────────────────────────────────────────────────────────

    private void BaueUI()
    {
        var bg = new ColorRect { Color = FmTheme.BgDark };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        FmTheme.SetMargin(margin, 28, 20);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        root.AddChild(BaueKopf());

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        root.AddChild(sep);

        _rumpf = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(_rumpf);
        _rumpf.AddChild(BaueSpielRumpf());

        root.AddChild(BaueLeiste());
    }

    private Control BaueKopf()
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);

        var stand = new HBoxContainer();
        stand.AddThemeConstantOverride("separation", 16);

        _heimNameLabel = FmTheme.MakeLabel("", 22, FmTheme.TextPrimary, HorizontalAlignment.Right);
        _heimNameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        stand.AddChild(_heimNameLabel);

        _ergebnisLabel = FmTheme.MakeLabel("0 : 0", 40, FmTheme.TextPrimary, HorizontalAlignment.Center);
        _ergebnisLabel.CustomMinimumSize = new Vector2(160, 0);
        stand.AddChild(_ergebnisLabel);

        _gastNameLabel = FmTheme.MakeLabel("", 22, FmTheme.TextPrimary);
        _gastNameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        stand.AddChild(_gastNameLabel);

        vbox.AddChild(stand);

        var uhrZeile = new HBoxContainer();
        uhrZeile.AddThemeConstantOverride("separation", 10);

        _halbzeitLabel = FmTheme.MakeLabel("1. Halbzeit", 13, FmTheme.TextSecondary);
        _halbzeitLabel.CustomMinimumSize = new Vector2(92, 0);
        uhrZeile.AddChild(_halbzeitLabel);

        // Der Balken traegt eine Marke bei 45 Minuten. Ohne sie war der Halbzeitpfiff auf der
        // Uhr nicht zu sehen - der Balken lief einfach durch.
        var uhrRahmen = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 12),
        };
        uhrZeile.AddChild(uhrRahmen);

        _uhr = new ProgressBar
        {
            MinValue = 0, MaxValue = Spielminuten, Value = 0, ShowPercentage = false,
        };
        _uhr.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _uhr.AddThemeStyleboxOverride("fill", FmTheme.KonditionsBalken(FmTheme.Success));
        _uhr.AddThemeStyleboxOverride("background", FmTheme.KonditionsBalken(FmTheme.BgToolbar));
        uhrRahmen.AddChild(_uhr);

        var marke = new ColorRect { Color = FmTheme.BgDark, MouseFilter = MouseFilterEnum.Ignore };
        marke.SetAnchorsPreset(LayoutPreset.CenterLeft);
        marke.AnchorLeft = 0.5f;
        marke.AnchorRight = 0.5f;
        marke.AnchorTop = 0f;
        marke.AnchorBottom = 1f;
        marke.OffsetLeft = -1;
        marke.OffsetRight = 1;
        marke.OffsetTop = 0;
        marke.OffsetBottom = 0;
        marke.TooltipText = "Halbzeit";
        uhrRahmen.AddChild(marke);

        _minuteLabel = FmTheme.MakeLabel("0'", 17, FmTheme.TextPrimary, HorizontalAlignment.Right);
        _minuteLabel.CustomMinimumSize = new Vector2(62, 0);
        uhrZeile.AddChild(_minuteLabel);

        vbox.AddChild(uhrZeile);
        return vbox;
    }

    /// <summary>Hebt das gewählte Tempo hervor.</summary>
    private void ZeigeTempo()
    {
        foreach (var (tempo, knopf) in _tempoKnoepfe)
        {
            bool aktiv = tempo == _spielfeldTempo;
            var stil = FmTheme.Flaeche(aktiv ? FmTheme.Accent : new Color(0, 0, 0, 0), 7);
            var schwebend = FmTheme.Flaeche(aktiv ? FmTheme.Accent : FmTheme.BgHover, 7);
            foreach (var zustand in new[] { "normal", "pressed", "focus" })
                knopf.AddThemeStyleboxOverride(zustand, stil);
            knopf.AddThemeStyleboxOverride("hover", schwebend);
            var farbe = aktiv ? FmTheme.AccentText : FmTheme.TextNav;
            knopf.AddThemeColorOverride("font_color", farbe);
            knopf.AddThemeColorOverride("font_pressed_color", farbe);
            knopf.AddThemeColorOverride("font_hover_color", aktiv ? FmTheme.AccentText : FmTheme.TextPrimary);
            knopf.AddThemeFontOverride("font", FmTheme.SchriftAnzeige);
            knopf.AddThemeFontSizeOverride("font_size", 16);
        }
    }

    private void BenenneMannschaften(LiveSpiel spiel)
    {
        _heimNameLabel.Text = spiel.HeimVerein ?? "";
        _heimNameLabel.AddThemeColorOverride("font_color",
            spiel.EigenesHeimspiel ? FmTheme.Accent : FmTheme.TextPrimary);
        _gastNameLabel.Text = spiel.GastVerein ?? "";
        _gastNameLabel.AddThemeColorOverride("font_color",
            !spiel.EigenesHeimspiel ? FmTheme.Accent : FmTheme.TextPrimary);
    }

    /// <summary>
    /// Alles auf einem Bildschirm: Aufstellungen aussen, in der Mitte der Platz und darunter
    /// Verlauf und Statistik nebeneinander.
    ///
    /// <para>Vorher lagen Verlauf, Platz und Statistik als Reiter uebereinander - man sah immer
    /// nur eines davon. Wer dem Spiel zusah, verpasste jedes Tor im Verlauf; wer den Verlauf
    /// las, verpasste das Spiel. Auf einem Bildschirm ist der Platz das Grosse, die beiden
    /// Tafeln darunter das Kleine.</para>
    /// </summary>
    private Control BaueSpielRumpf()
    {
        var mitte = new HBoxContainer();
        mitte.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        mitte.AddThemeConstantOverride("separation", 14);

        mitte.AddChild(BaueTeamSpalte(out _heimSpalte, out _heimStaerkeLabel));

        var spalte = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        spalte.AddThemeConstantOverride("separation", 8);
        mitte.AddChild(spalte);

        // Der Platz bekommt drei Viertel der Hoehe, die beiden Tafeln teilen sich den Rest.
        var feld = BaueSpielfeld();
        feld.SizeFlagsVertical = SizeFlags.ExpandFill;
        feld.SizeFlagsStretchRatio = 3.0f;
        spalte.AddChild(feld);

        var tafeln = new HBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 1.0f,
        };
        tafeln.AddThemeConstantOverride("separation", 12);
        spalte.AddChild(tafeln);

        _logScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        tafeln.AddChild(BaueTafel("Verlauf", _logScroll));
        _log = new VBoxContainer();
        _log.AddThemeConstantOverride("separation", 5);
        _log.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _logScroll.AddChild(_log);

        var statistikScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        tafeln.AddChild(BaueTafel("Statistik", statistikScroll));
        _statistik = new StatistikTafel { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        statistikScroll.AddChild(_statistik);

        mitte.AddChild(BaueTeamSpalte(out _gastSpalte, out _gastStaerkeLabel));
        return mitte;
    }

    /// <summary>Ein beschriftetes Feld - die Ueberschrift ersetzt den weggefallenen Reiter.</summary>
    private static Control BaueTafel(string titel, Control inhalt)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());

        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 12, 8);
        panel.AddChild(rand);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        rand.AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel(titel, 12, FmTheme.TextSecondary));
        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        vbox.AddChild(sep);
        vbox.AddChild(inhalt);
        return panel;
    }

    /// <summary>
    /// Der Platz samt Tempowahl und Halbzeitschild.
    ///
    /// <para>Das Spielfeld gibt den Takt der ganzen Ansicht vor: Die Spielminute ergibt sich
    /// daraus, was gerade zu sehen ist, und der Ticker der alten Engine folgt ihr. Der
    /// Tempowaehler ist deshalb die Geschwindigkeit der Partie, nicht nur die des Bildes.</para>
    /// </summary>
    private Control BaueSpielfeld()
    {
        var spalte = new VBoxContainer();
        spalte.AddThemeConstantOverride("separation", 6);

        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 8);
        spalte.AddChild(kopf);

        _spielfeldHinweis = FmTheme.MakeLabel("Agenten-Engine", 12, FmTheme.TextSecondary);
        _spielfeldHinweis.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(_spielfeldHinweis);

        // Wer auf welches Tor spielt. Zur Halbzeit dreht sich das um - damit ist der
        // Seitenwechsel nicht nur an den Punkten auf dem Rasen zu erkennen.
        _richtungLabel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary,
            HorizontalAlignment.Right);
        kopf.AddChild(_richtungLabel);

        // Eine Partie dauert bei einfachem Tempo neunzig Minuten, bei zwanzigfachem
        // viereinhalb. Wer gar nicht zusehen will, nimmt "Ueberspringen" in der Fußleiste.
        // Als Segmentschalter wie die Formation in der Aufstellung: Das gewählte Tempo ist auf
        // einen Blick zu erkennen. Vorher sahen alle drei Knöpfe gleich aus.
        var segment = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var segmentStil = FmTheme.Flaeche(FmTheme.BgPanel, 10, FmTheme.Border);
        segmentStil.SetContentMarginAll(3);
        segment.AddThemeStyleboxOverride("panel", segmentStil);
        var segmente = new HBoxContainer();
        segmente.AddThemeConstantOverride("separation", 3);
        segment.AddChild(segmente);
        foreach (double tempo in new[] { 1.0, 5.0, 20.0 })
        {
            var knopf = new Button
            {
                Text = $"{tempo:0}x",
                CustomMinimumSize = new Vector2(48, 30),
                FocusMode = FocusModeEnum.None,
                TooltipText = tempo == 1.0 ? "Echtzeit" : $"{tempo:0}-fache Geschwindigkeit",
            };
            knopf.Pressed += () =>
            {
                _spielfeldTempo = tempo;
                ZeigeTempo();
            };
            _tempoKnoepfe[tempo] = knopf;
            segmente.AddChild(knopf);
        }
        kopf.AddChild(segment);
        ZeigeTempo();

        // Platz und Halbzeitschild liegen uebereinander: Waehrend der Pause steht das Schild
        // mitten auf dem Rasen, damit die Halbzeit nicht nur eine Zeile im Verlauf ist.
        var buehne = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        spalte.AddChild(buehne);

        _spielfeld = new Spielfeld();
        _spielfeld.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        buehne.AddChild(_spielfeld);

        buehne.AddChild(BauePausenschild());
        return spalte;
    }

    /// <summary>Das Schild, das waehrend der Halbzeitpause auf dem Platz steht.</summary>
    private Control BauePausenschild()
    {
        var mitte = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        mitte.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _pausenschild = new Panel { Visible = false, CustomMinimumSize = new Vector2(260, 0) };
        var stil = new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0.72f) };
        stil.SetBorderWidthAll(1);
        stil.BorderColor = FmTheme.Border;
        stil.SetCornerRadiusAll(6);
        stil.SetContentMarginAll(16);
        _pausenschild.AddThemeStyleboxOverride("panel", stil);
        mitte.AddChild(_pausenschild);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _pausenschild.AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel("HALBZEIT", 22, FmTheme.TextPrimary,
            HorizontalAlignment.Center));
        _pausenText = FmTheme.MakeLabel("", 14, FmTheme.TextSecondary, HorizontalAlignment.Center);
        vbox.AddChild(_pausenText);
        return mitte;
    }

    private Control BaueTeamSpalte(out VBoxContainer spielerBox, out Label staerkeLabel)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(280, 0) };
        var style = new StyleBoxFlat { BgColor = FmTheme.BgToolbar };
        style.SetBorderWidthAll(1);
        style.SetContentMarginAll(0);
        panel.AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 12, 10);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 3);
        margin.AddChild(vbox);

        staerkeLabel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        staerkeLabel.TooltipText = "Elf-Stärke bei Anpfiff gegen die aktuelle Stärke auf dem Platz - "
            + "sinkt mit der Ermüdung, springt bei Wechseln und Platzverweisen.";
        vbox.AddChild(staerkeLabel);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        vbox.AddChild(sep);

        spielerBox = new VBoxContainer();
        spielerBox.AddThemeConstantOverride("separation", 2);
        vbox.AddChild(spielerBox);

        return panel;
    }

    private HBoxContainer BaueLeiste()
    {
        var leiste = new HBoxContainer();
        leiste.AddThemeConstantOverride("separation", 10);

        _wechselLabel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        _wechselLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leiste.AddChild(_wechselLabel);

        _wechselButton = new Button { Text = "🔄  Wechseln", CustomMinimumSize = new Vector2(160, 40) };
        FmTheme.ApplyButton(_wechselButton, FmTheme.BgPanel);
        _wechselButton.AddThemeColorOverride("font_color", FmTheme.TextPrimary);
        _wechselButton.Pressed += OnWechselnPressed;
        leiste.AddChild(_wechselButton);

        _aktionButton = new Button { Text = "⏩  Überspringen", CustomMinimumSize = new Vector2(190, 40) };
        FmTheme.ApplyButton(_aktionButton, FmTheme.BgPanel);
        _aktionButton.Pressed += OnAktionPressed;
        leiste.AddChild(_aktionButton);

        return leiste;
    }

    private void ZeigeFehler(string text)
    {
        foreach (Node kind in _rumpf.GetChildren()) kind.QueueFree();

        var box = new CenterContainer();
        box.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        box.AddChild(FmTheme.MakeLabel(text, 15, FmTheme.Danger));
        _rumpf.AddChild(box);

        _wechselButton.Visible = false;
        _aktionButton.Text = "← Zurück";
        _aktionButton.Disabled = false;
        _abgepfiffen = true;
    }

    // ── Ablauf ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Die Spielminute, wie sie fortlaufend von 0 bis 90 zählt.
    ///
    /// <para>Die Anzeigetafel fängt die zweite Halbzeit wieder bei 45:00 an - richtig für den
    /// Zuschauer, aber nicht monoton: Nach sieben Minuten Nachspielzeit steht dort 52', dann
    /// wieder 45'. Der Ticker der alten Engine braucht dagegen eine Zahl, die nur wächst, sonst
    /// bliebe er bei 52 stehen, bis die zweite Halbzeit dort wieder ankommt.</para>
    /// </summary>
    private double Spielminute(double anzeigezeit)
    {
        double minute = anzeigezeit / 60.0;
        return _halbzeit == 1
            ? Math.Min(minute, Halbzeitminuten)
            : Halbzeitminuten + Math.Min(Math.Max(minute - Halbzeitminuten, 0), Halbzeitminuten);
    }

    /// <summary>Kopfzeile aus dem, was gerade auf dem Platz zu sehen ist.</summary>
    private void ZeigeUhr(SzenenPlayer.Bild bild)
    {
        double minute = Spielminute(bild.Anzeigezeit);
        _uhr.Value = minute;
        _minuteLabel.Text = MinutenText(bild.Anzeigezeit);
        _halbzeitLabel.Text = bild.Pause
            ? "Halbzeitpause"
            : _halbzeit == 1 ? "1. Halbzeit" : "2. Halbzeit";

        if (_pausenschild.Visible != bild.Pause)
        {
            _pausenschild.Visible = bild.Pause;
            _pausenText.Text = $"{_feldHeimTore} : {_feldGastTore}\nSeitenwechsel";
        }
    }

    /// <summary>
    /// Die Minute, wie sie auf der Anzeigetafel steht - mit Nachspielzeit als "45+3'".
    /// </summary>
    private string MinutenText(double anzeigezeit)
    {
        int minute = (int)(anzeigezeit / 60.0) + 1;
        int regulaer = _halbzeit == 1 ? Halbzeitminuten : Spielminuten;
        return minute > regulaer ? $"{regulaer}+{minute - regulaer}'" : $"{minute}'";
    }

    /// <summary>
    /// Zieht die Partie auf die Minute nach, die auf dem Spielfeld gerade läuft.
    ///
    /// <para>Das Spielfeld ist die Uhr, der Ticker folgt ihr. Damit stimmen Spielstand,
    /// Verlauf und Bild zum ersten Mal überein - vorher liefen sie mit dem Faktor 300
    /// auseinander.</para>
    /// </summary>
    private void TreibeDiePartie(double spielminute)
    {
        if (_wartetAufServer || _abgepfiffen || _spiel == null) return;

        int minute = Math.Min((int)spielminute, Spielminuten);
        if (minute <= _gerechneteMinute) return;

        _gerechneteMinute = minute;
        _wartetAufServer = true;
        _ = HoleStand(minute);
    }

    /// <summary>
    /// Holt einmal je Spielminute den Stand nach: Aufstellungen, Kondition, Wechselkonto.
    ///
    /// <para>Ein reiner Lesezugriff. Gerechnet wird die Partie ausschließlich über die
    /// Taktpakete - es gibt nur noch eine Engine, und sie läuft im Takt des Spielfelds.
    /// Vorher trieb dieser Aufruf eine zweite Simulation an, die ihr eigenes Ergebnis
    /// produzierte; genau daher kam der Widerspruch zwischen Anzeigetafel und Statistik.</para>
    /// </summary>
    private async Task HoleStand(int minute)
    {
        var zustand = await ApiClient.GetAsync<LiveSpiel>("spielfeld/stand");
        _wartetAufServer = false;

        if (!Lebt) return;

        if (zustand == null)
        {
            _pausiert = true;
            SchreibeZeile(_spiel?.Minute ?? 0, "Verbindung zum Spiel verloren.", FmTheme.Danger);
            _aktionButton.Text = "← Zurück";
            _abgepfiffen = true;
            return;
        }

        Aktualisiere(zustand);
        if (minute >= Spielminuten) await Abpfiff();
    }

    /// <summary>
    /// Übernimmt den Zustand der alten Engine: Aufstellungen, Kondition, Wechsel, Statistik.
    ///
    /// <para>Uhr, Spielstand und Verlauf kommen dagegen aus der Agenten-Partie - aus dem, was
    /// auf dem Platz zu sehen ist. Beides nebeneinander stehen zu lassen war der Grund, warum
    /// ein Tor auf dem Rasen nirgends auf der Anzeigetafel auftauchte.</para>
    /// </summary>
    private void Aktualisiere(LiveSpiel zustand)
    {
        _spiel = zustand;

        _wechselLabel.Text = zustand.WechselHinweis;
        _wechselButton.Disabled = !zustand.DarfWechseln || _abgepfiffen;

        _heimStaerkeLabel.Text = StaerkeHeaderText(zustand.HeimStaerke, zustand.HeimStaerkeAktuell);
        _heimStaerkeLabel.AddThemeColorOverride("font_color",
            StaerkeFarbe(zustand.HeimStaerke, zustand.HeimStaerkeAktuell));
        _gastStaerkeLabel.Text = StaerkeHeaderText(zustand.GastStaerke, zustand.GastStaerkeAktuell);
        _gastStaerkeLabel.AddThemeColorOverride("font_color",
            StaerkeFarbe(zustand.GastStaerke, zustand.GastStaerkeAktuell));

        FuelleSpalte(_heimSpalte, zustand.HeimAufstellung);
        FuelleSpalte(_gastSpalte, zustand.GastAufstellung);

        // Zu Spielbeginn stehen fast alle Zeilen auf null - die blendet die Tafel aus, damit
        // dort nicht minutenlang eine Reihe Nullen steht.
        _statistik.Zeige(zustand.HeimStatistik, zustand.GastStatistik, nurGefuellte: true);
    }

    private void FuelleSpalte(VBoxContainer box, List<LiveSpieler> spieler)
    {
        foreach (Node kind in box.GetChildren()) kind.QueueFree();

        foreach (var s in spieler)
        {
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 6);

            var slot = FmTheme.MakeLabel(s.Slot, 11, FmTheme.TextSecondary);
            slot.CustomMinimumSize = new Vector2(42, 0);
            zeile.AddChild(slot);

            var name = FmTheme.MakeLabel(s.Name, 11,
                s.AufDemPlatz ? FmTheme.TextPrimary : FmTheme.TextSecondary);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            zeile.AddChild(name);

            var staerke = FmTheme.MakeLabel(s.EffektiveStaerke.ToString(), 11,
                s.AufDemPlatz ? StaerkeFarbeSpieler(s.Staerke, s.EffektiveStaerke) : FmTheme.TextSecondary,
                HorizontalAlignment.Right);
            staerke.CustomMinimumSize = new Vector2(24, 0);
            staerke.TooltipText = s.AufDemPlatz
                ? s.Erklaerung
                : StaerkeErklaerung.Basis(s.Position, s.Grundstaerke, s.Eingespieltheit, s.Staerke);
            zeile.AddChild(staerke);

            if (s.AufDemPlatz && s.StaerkeVerlust >= 3)
            {
                var verlust = FmTheme.MakeLabel($"▼{s.StaerkeVerlust}", 10, FmTheme.Danger);
                verlust.CustomMinimumSize = new Vector2(22, 0);
                zeile.AddChild(verlust);
            }

            var frische = new ProgressBar
            {
                MinValue = 0, MaxValue = 100,
                Value = s.AufDemPlatz ? s.Kondition : 0,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(46, 8),
            };
            frische.AddThemeStyleboxOverride("fill",
                FmTheme.KonditionsBalken(FmTheme.FrischeFarbe(s.Kondition)));
            frische.AddThemeStyleboxOverride("background", FmTheme.KonditionsBalken(FmTheme.BgDark));
            zeile.AddChild(frische);

            box.AddChild(zeile);
        }
    }

    /// <summary>Farbe der Stärke-Zahl eines Spielers, nach Anteil des ermüdungsbedingten Verlusts.</summary>
    private static Color StaerkeFarbeSpieler(int basis, int effektiv)
    {
        if (basis <= 0) return FmTheme.TextPrimary;
        double anteil = (double)effektiv / basis;
        return anteil switch
        {
            >= 0.95 => FmTheme.TextPrimary,
            >= 0.85 => FmTheme.Gold,
            _       => FmTheme.Danger,
        };
    }

    private static string StaerkeHeaderText(double kickoff, double aktuell)
    {
        string de(double w) => w.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"));
        double delta = aktuell - kickoff;
        if (Math.Abs(delta) < 0.1) return $"Stärke {de(kickoff)}";
        string vorzeichen = delta > 0 ? "+" : "−";
        return $"Stärke {de(aktuell)}  ({vorzeichen}{de(Math.Abs(delta))})";
    }

    /// <summary>Farbe der Elf-Stärke-Zeile im Kopf, nach Abstand zum Wert bei Anpfiff.</summary>
    private static Color StaerkeFarbe(double kickoff, double aktuell)
    {
        if (kickoff <= 0) return FmTheme.TextSecondary;
        double anteil = aktuell / kickoff;
        return anteil switch
        {
            >= 0.95 => FmTheme.TextSecondary,
            >= 0.85 => FmTheme.Gold,
            _       => FmTheme.Danger,
        };
    }

    // ── Auswechseln ──────────────────────────────────────────────────────────

    private void OnWechselnPressed()
    {
        if (_abgepfiffen || _spiel == null) return;

        _pausiert = true; // Uhr steht, solange gewählt wird - auch auf dem Platz
        WechselDialog.Zeige(this, _spiel,
            beiBestaetigung: async paare => await FuehreWechselAus(paare),
            beiAbbruch: () => { if (!_abgepfiffen) _pausiert = false; });
    }

    /// <summary>
    /// Führt alle geplanten Wechsel nacheinander aus. Da die Uhr steht, geschieht das in
    /// derselben Spielminute und zählt damit als ein Wechselfenster.
    /// </summary>
    private async Task FuehreWechselAus(List<(long Raus, long Rein)> paare)
    {
        LiveSpiel? zustand = null;
        foreach (var (raus, rein) in paare)
        {
            zustand = await ApiClient.PostAsync<object, LiveSpiel>(
                $"spielfeld/wechsel?raus={raus}&rein={rein}", new { });
            if (!Lebt) return;
            if (zustand == null) break;
        }
        if (zustand != null) Aktualisiere(zustand);
        if (!_abgepfiffen) _pausiert = false;
    }

    // ── Abpfiff ──────────────────────────────────────────────────────────────

    private async Task Abpfiff()
    {
        if (_abgepfiffen) return;
        _abgepfiffen = true;
        _pausiert = true;
        _spielfeldLaeuft = false;
        _wechselButton.Disabled = true;

        var zustand = await ApiClient.PostAsync<object, LiveSpiel>("spielfeld/abpfiff", new { });
        if (!Lebt) return;

        if (zustand != null)
        {
            Aktualisiere(zustand);
        }

        SchreibeZeile(Spielminuten,
            $"⏹  Abpfiff  –  Endstand {_spiel?.HeimTore} : {_spiel?.GastTore}",
            FmTheme.TextPrimary);
        _uhr.Value = Spielminuten;
        _uhr.AddThemeStyleboxOverride("fill", FmTheme.KonditionsBalken(FmTheme.Accent));
        _pausenschild.Visible = false;

        // Der restliche Spieltag läuft im Hintergrund - erst danach sind Tabelle und
        // Statistiken stimmig, deshalb wird bis dahin gewartet.
        _spieltagLaeuft = true;
        _wechselButton.Visible = false;
        _aktionButton.Disabled = true;
        _aktionButton.Text = "Restlicher Spieltag …";
        await WarteAufSpieltag();
        if (!Lebt) return;
        _spieltagLaeuft = false;

        await ZeigeZusammenfassung();
    }

    private async Task WarteAufSpieltag()
    {
        for (int i = 0; i < 120; i++)
        {
            var stand = await ApiClient.GetAsync<Fortschritt>("spiel/spieltag/fortschritt");
            if (!Lebt) return;
            if (stand is { Fertig: true }) return;

            var baum = GetTree();
            if (baum == null) return;
            await ToSignal(baum.CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            if (!Lebt) return;
        }
    }

    // ── Zusammenfassung ──────────────────────────────────────────────────────

    /// <summary>
    /// Löst die Live-Ansicht durch die Nachbetrachtung ab: Kennzahlen der Partie und die Noten
    /// beider Mannschaften. Die Daten kommen aus dem Spielbericht, nicht aus dem Live-Zustand -
    /// die Noten entstehen erst beim Speichern.
    /// </summary>
    private async Task ZeigeZusammenfassung()
    {
        _aktionButton.Text = "← Zurück zum Verein";
        _aktionButton.Disabled = false;

        long? spielId = _spiel?.SpielId;
        var bericht = spielId == null
            ? null
            : await ApiClient.GetAsync<SpielBericht>($"spiel/{spielId}/bericht");

        if (!Lebt) return;

        foreach (Node kind in _rumpf.GetChildren()) kind.QueueFree();

        if (bericht == null)
        {
            var hinweis = new CenterContainer();
            hinweis.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            hinweis.AddChild(FmTheme.MakeLabel(
                "Die Zusammenfassung konnte nicht geladen werden.", 14, FmTheme.TextSecondary));
            _rumpf.AddChild(hinweis);
            return;
        }

        var scroll = new ScrollContainer();
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _rumpf.AddChild(scroll);

        var inhalt = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inhalt.AddThemeConstantOverride("separation", 18);
        scroll.AddChild(inhalt);

        inhalt.AddChild(FmTheme.MakeLabel("Spielbericht", 18, FmTheme.TextPrimary));

        var tafel = new StatistikTafel { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inhalt.AddChild(tafel);
        tafel.Zeige(bericht.HeimStatistik, bericht.GastStatistik);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        inhalt.AddChild(sep);

        inhalt.AddChild(FmTheme.MakeLabel("Noten", 18, FmTheme.TextPrimary));

        var noten = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        noten.AddThemeConstantOverride("separation", 18);
        inhalt.AddChild(noten);

        noten.AddChild(BaueNotenSpalte(bericht.HeimVerein ?? "Heim", bericht.HeimAufstellung,
            bericht.HeimVereinId == _spiel?.EigenerVereinId));
        noten.AddChild(BaueNotenSpalte(bericht.GastVerein ?? "Gast", bericht.GastAufstellung,
            bericht.GastVereinId == _spiel?.EigenerVereinId));
    }

    private static Control BaueNotenSpalte(string verein, List<AufstellungsSpieler> spieler,
                                           bool eigener)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 16, 12);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 5);
        margin.AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel(verein, 15,
            eigener ? FmTheme.Accent : FmTheme.TextPrimary));

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        vbox.AddChild(sep);

        foreach (var s in spieler)
        {
            vbox.AddChild(BaueNotenZeile(s));
        }
        return panel;
    }

    private static Control BaueNotenZeile(AufstellungsSpieler s)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 8);

        var slot = FmTheme.MakeLabel(s.Slot, 11, FmTheme.TextSecondary);
        slot.CustomMinimumSize = new Vector2(44, 0);
        zeile.AddChild(slot);

        var name = FmTheme.MakeLabel(s.Name, 12,
            s.Startelf ? FmTheme.TextPrimary : FmTheme.TextSecondary);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        zeile.AddChild(name);

        // Tore, Vorlagen und Karten als Symbole - eine Zahlenspalte je Kategorie wäre in einer
        // Notenübersicht mehr Tabelle als Information.
        var marken = FmTheme.MakeLabel(Marken(s), 12, FmTheme.TextSecondary);
        marken.CustomMinimumSize = new Vector2(70, 0);
        zeile.AddChild(marken);

        var einsatz = FmTheme.MakeLabel(EinsatzText(s), 11, FmTheme.TextSecondary,
            HorizontalAlignment.Right);
        einsatz.CustomMinimumSize = new Vector2(62, 0);
        einsatz.TooltipText = "Einsatzzeit";
        zeile.AddChild(einsatz);

        var note = FmTheme.MakeLabel(NoteText(s.Note), 14, NotenFarbe(s.Note),
            HorizontalAlignment.Right);
        note.CustomMinimumSize = new Vector2(38, 0);
        note.TooltipText = "Note von 1 bis 10 - höher ist besser, 6 ist ein durchschnittlicher Auftritt.";
        zeile.AddChild(note);

        return zeile;
    }

    private static string Marken(AufstellungsSpieler s)
    {
        string text = "";
        for (int i = 0; i < s.Tore; i++)        text += "⚽";
        for (int i = 0; i < s.Vorlagen; i++)    text += "🅰";
        for (int i = 0; i < s.GelbeKarten; i++) text += "🟨";
        for (int i = 0; i < s.RoteKarten; i++)  text += "🟥";
        return text;
    }

    /// <summary>Einsatzzeit als Spanne; wer durchspielt, bekommt nur die Dauer.</summary>
    private static string EinsatzText(AufstellungsSpieler s)
    {
        int bis = s.BisMinute ?? Spielminuten;
        if (s.VonMinute == 0 && s.BisMinute == null) return "90'";
        return $"{s.VonMinute}'–{bis}'";
    }

    private static string NoteText(double note) =>
        note.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"));

    /// <summary>Die Note läuft von 1 bis 10, wobei 6 ein durchschnittlicher Auftritt ist.</summary>
    private static Color NotenFarbe(double note) => note switch
    {
        >= 7.5 => FmTheme.Success,
        >= 6.5 => FmTheme.Gold,
        >= 5.0 => FmTheme.TextPrimary,
        _      => FmTheme.Danger,
    };

    // ── Protokoll ────────────────────────────────────────────────────────────

    private void SchreibeZeile(int minute, string text, Color farbe)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);

        var min = FmTheme.MakeLabel($"{minute}'", 13, FmTheme.TextSecondary, HorizontalAlignment.Right);
        min.CustomMinimumSize = new Vector2(40, 0);
        zeile.AddChild(min);

        var inhalt = FmTheme.MakeLabel(text, 13, farbe);
        inhalt.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        zeile.AddChild(inhalt);

        _log.AddChild(zeile);
        CallDeferred(nameof(ScrolleAnsEnde));
    }

    private void ScrolleAnsEnde()
    {
        if (!IsInstanceValid(_logScroll)) return;
        var leiste = _logScroll.GetVScrollBar();
        if (leiste != null) _logScroll.ScrollVertical = (int)leiste.MaxValue;
    }

    // ── Steuerung ────────────────────────────────────────────────────────────

    private async void OnAktionPressed()
    {
        if (_spieltagLaeuft) return;

        if (_abgepfiffen)
        {
            ZurueckZumVerein();
            return;
        }

        // Überspringen: Das Spielfeld hält an, den Rest rechnet der Abpfiff in einem Zug.
        _pausiert = true;
        _spielfeldLaeuft = false;
        _aktionButton.Disabled = true;
        await Abpfiff();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape }
            && _abgepfiffen && !_spieltagLaeuft)
        {
            ZurueckZumVerein();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ZurueckZumVerein()
    {
        _pausiert = true;
        _spielfeldLaeuft = false;
        GetTree().ChangeSceneToFile(SceneGameMain);
    }
}
