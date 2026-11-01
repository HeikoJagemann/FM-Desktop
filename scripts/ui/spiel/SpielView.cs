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
    private Wappen _heimWappen = null!;
    private Wappen _gastWappen = null!;

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

    /// <summary>Wie viele Tore und Karten der Verlauf schon zeigt - beim Überspringen kommt nur der Rest nach.</summary>
    private int _geschriebeneTore;
    private int _geschriebeneKarten;

    /// <summary>Bis zu welcher Minute die Partie schon gerechnet ist.</summary>
    private int _gerechneteMinute;

    /// <summary>Wie viele Abrufe hintereinander nichts geliefert haben.</summary>
    private int _fehlschlaege;

    /// <summary>Woran es gerade liegt, wenn das Bild steht - fuer die Kopfzeile.</summary>
    private string _spielfeldLage = "";

    // Fußleiste
    private Label _wechselLabel = null!;
    private Button _wechselButton = null!;
    private Button _taktikButton = null!;
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
        if (v.Art is "TOR" or "EIGENTOR") _geschriebeneTore++;
        if (v.Art is "GELB" or "GELBROT" or "ROT") _geschriebeneKarten++;
        switch (v.Art)
        {
            case "TOR":
                SchreibeZeile(v.Minute, string.IsNullOrEmpty(v.Vorlage)
                        ? $"Tor - {v.Name}"
                        : $"Tor - {v.Name}  (Vorlage {v.Vorlage})",
                    eigenes ? FmTheme.Success : FmTheme.Danger, Symbol.Art.Ball);
                break;
            case "EIGENTOR":
                SchreibeZeile(v.Minute, $"Eigentor - {v.Name}",
                    eigenes ? FmTheme.Success : FmTheme.Danger, Symbol.Art.Ball);
                break;
            case "GELB":
                SchreibeZeile(v.Minute, $"Gelbe Karte - {v.Name}", FmTheme.Gold, Symbol.Art.Gelb);
                break;
            case "GELBROT":
                SchreibeZeile(v.Minute, $"Gelb-Rot - {v.Name}", FmTheme.Danger, Symbol.Art.GelbRot);
                break;
            case "ROT":
                SchreibeZeile(v.Minute, $"Rote Karte - {v.Name}", FmTheme.Danger, Symbol.Art.Rot);
                break;
            case "HALBZEIT":
                // Der Pfiff nach 90 Minuten in einem K.-o.-Spiel führt in die Verlängerung.
                string pause = v.Minute switch
                {
                    90  => $"Nach 90 Minuten {_feldHeimTore} : {_feldGastTore} - Verlängerung",
                    105 => $"Halbzeit der Verlängerung - {_feldHeimTore} : {_feldGastTore}, Seitenwechsel",
                    _   => $"Halbzeit - {_feldHeimTore} : {_feldGastTore}, Seitenwechsel",
                };
                SchreibeZeile(v.Minute, pause, FmTheme.TextPrimary, Symbol.Art.Pause);
                break;
            case "ABPFIFF":
                SchreibeZeile(v.Minute,
                    $"Abpfiff - {_feldHeimTore} : {_feldGastTore}", FmTheme.TextPrimary, Symbol.Art.Abpfiff);
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

        _rumpf = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(_rumpf);
        _rumpf.AddChild(BaueSpielRumpf());

        root.AddChild(BaueLeiste());
    }

    /// <summary>
    /// Die Anzeigetafel: Wappen, Vereine, Spielstand und darunter die Uhr. Der eigene Verein
    /// trägt den Bernstein-Ring - nicht mehr eine andersfarbige Schrift.
    /// </summary>
    private Control BaueKopf()
    {
        var tafel = new PanelContainer();
        var tafelStil = FmTheme.PanelStyle(14);
        tafelStil.ContentMarginLeft = tafelStil.ContentMarginRight = 24;
        tafelStil.ContentMarginTop = 14;
        tafelStil.ContentMarginBottom = 12;
        tafel.AddThemeStyleboxOverride("panel", tafelStil);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        tafel.AddChild(vbox);

        var stand = new HBoxContainer();
        stand.AddThemeConstantOverride("separation", 16);

        _heimNameLabel = FmTheme.MakeTitel("", 28);
        _heimNameLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _heimNameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _heimNameLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        stand.AddChild(_heimNameLabel);

        _heimWappen = Wappen.Neu("", 48, FmTheme.BorderStark);
        stand.AddChild(_heimWappen);

        _ergebnisLabel = FmTheme.MakeZahl("0 : 0", 54);
        _ergebnisLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _ergebnisLabel.CustomMinimumSize = new Vector2(170, 0);
        stand.AddChild(_ergebnisLabel);

        _gastWappen = Wappen.Neu("", 48, FmTheme.BorderStark);
        stand.AddChild(_gastWappen);

        _gastNameLabel = FmTheme.MakeTitel("", 28);
        _gastNameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _gastNameLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        stand.AddChild(_gastNameLabel);

        vbox.AddChild(stand);

        var uhrZeile = new HBoxContainer();
        uhrZeile.AddThemeConstantOverride("separation", 12);

        _halbzeitLabel = FmTheme.MakeLabel("1. Halbzeit", 12, FmTheme.TextSecondary);
        _halbzeitLabel.AddThemeFontOverride("font", FmTheme.SchriftUi(600));
        _halbzeitLabel.CustomMinimumSize = new Vector2(92, 0);
        _halbzeitLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        uhrZeile.AddChild(_halbzeitLabel);

        // Der Balken traegt eine Marke bei 45 Minuten. Ohne sie war der Halbzeitpfiff auf der
        // Uhr nicht zu sehen - der Balken lief einfach durch.
        var uhrRahmen = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 8),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        uhrZeile.AddChild(uhrRahmen);

        _uhr = new ProgressBar
        {
            MinValue = 0, MaxValue = Spielminuten, Value = 0, ShowPercentage = false,
        };
        _uhr.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _uhr.AddThemeStyleboxOverride("fill", FmTheme.KonditionsBalken(FmTheme.Success));
        _uhr.AddThemeStyleboxOverride("background", FmTheme.KonditionsBalken(FmTheme.Border));
        uhrRahmen.AddChild(_uhr);

        var marke = new ColorRect { Color = FmTheme.BgPanel, MouseFilter = MouseFilterEnum.Ignore };
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

        _minuteLabel = FmTheme.MakeZahl("0'", 24);
        _minuteLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _minuteLabel.CustomMinimumSize = new Vector2(70, 0);
        uhrZeile.AddChild(_minuteLabel);

        vbox.AddChild(uhrZeile);
        return tafel;
    }

    private void BenenneMannschaften(LiveSpiel spiel)
    {
        _heimNameLabel.Text = (spiel.HeimVerein ?? "").ToUpperInvariant();
        _gastNameLabel.Text = (spiel.GastVerein ?? "").ToUpperInvariant();
        _heimWappen.Setze(spiel.HeimVerein ?? "");
        _gastWappen.Setze(spiel.GastVerein ?? "");
        _heimWappen.Rand = spiel.EigenesHeimspiel ? FmTheme.Accent : FmTheme.BorderStark;
        _gastWappen.Rand = !spiel.EigenesHeimspiel ? FmTheme.Accent : FmTheme.BorderStark;
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
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle(14));

        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 16, 16, 10, 10);
        panel.AddChild(rand);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        rand.AddChild(vbox);

        vbox.AddChild(FmTheme.MakeAbschnitt(titel, 17));
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
        double[] tempi = { 1.0, 5.0, 20.0 };
        var segment = Segmentschalter.Neu(
            System.Array.ConvertAll(tempi, t => $"{t:0}x"),
            System.Array.IndexOf(tempi, _spielfeldTempo), new Vector2(48, 30));
        for (int i = 0; i < tempi.Length; i++)
            segment.Knopf(i).TooltipText = tempi[i] == 1.0 ? "Echtzeit" : $"{tempi[i]:0}-fache Geschwindigkeit";
        segment.Gewaehlt += i => _spielfeldTempo = tempi[i];
        kopf.AddChild(segment);

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

        _pausenschild = new Panel { Visible = false, CustomMinimumSize = new Vector2(280, 0) };
        var stil = FmTheme.Flaeche(new Color(0.035f, 0.055f, 0.047f, 0.88f), 14, FmTheme.BorderStark);
        stil.SetContentMarginAll(18);
        _pausenschild.AddThemeStyleboxOverride("panel", stil);
        mitte.AddChild(_pausenschild);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _pausenschild.AddChild(vbox);

        var halbzeit = FmTheme.MakeTitel("Halbzeit", 32);
        halbzeit.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(halbzeit);
        _pausenText = FmTheme.MakeLabel("", 14, FmTheme.TextSecondary, HorizontalAlignment.Center);
        vbox.AddChild(_pausenText);
        return mitte;
    }

    private Control BaueTeamSpalte(out VBoxContainer spielerBox, out Label staerkeLabel)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(280, 0) };
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle(14));

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 14, 14, 12, 12);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vbox);

        staerkeLabel = FmTheme.MakeLabel("", 13, FmTheme.TextSecondary);
        staerkeLabel.AddThemeFontOverride("font", FmTheme.SchriftUi(600));
        staerkeLabel.TooltipText = "Elf-Stärke bei Anpfiff gegen die aktuelle Stärke auf dem Platz - "
            + "sinkt mit der Ermüdung, springt bei Wechseln und Platzverweisen.";
        vbox.AddChild(staerkeLabel);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        vbox.AddChild(sep);

        spielerBox = new VBoxContainer();
        spielerBox.AddThemeConstantOverride("separation", 5);
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

        _wechselButton = new Button { Text = "Wechseln", CustomMinimumSize = new Vector2(160, 44) };
        FmTheme.ApplySecondaryButton(_wechselButton);
        _wechselButton.Pressed += OnWechselnPressed;
        leiste.AddChild(_wechselButton);

        _taktikButton = new Button { Text = "Umstellen", CustomMinimumSize = new Vector2(160, 44) };
        FmTheme.ApplySecondaryButton(_taktikButton);
        _taktikButton.TooltipText = "Formation und Taktik ändern - jederzeit, nur für diese Partie.";
        _taktikButton.Pressed += async () => await OeffneTaktik();
        leiste.AddChild(_taktikButton);

        _aktionButton = new Button { Text = "Überspringen", CustomMinimumSize = new Vector2(190, 44) };
        FmTheme.ApplySecondaryButton(_aktionButton);
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
        _taktikButton.Visible = false;
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

            var slot = FmTheme.MakeLabel(s.Slot, 11, s.AufDemPlatz
                ? FmTheme.TextFuerGruppe(PositionsgruppeHelfer.Von(s.Position))
                : FmTheme.TextGedimmt);
            slot.AddThemeFontOverride("font", FmTheme.SchriftUi(700));
            slot.CustomMinimumSize = new Vector2(38, 0);
            zeile.AddChild(slot);

            var name = FmTheme.MakeLabel(s.Name, 12,
                s.AufDemPlatz ? FmTheme.TextPrimary : FmTheme.TextGedimmt);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.ClipText = true;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            zeile.AddChild(name);

            // Einen Gegner kennt der Manager nur als Spanne aus seinen Berichten - ohne
            // Eingespieltheit und ohne Ermüdung in Zahlen.
            var staerke = s.Geschaetzt
                ? FmTheme.MakeLabel($"{s.StaerkeVon}–{s.StaerkeBis}", 12,
                    s.AufDemPlatz ? FmTheme.TextSecondary : FmTheme.TextGedimmt)
                : FmTheme.MakeZahl(s.EffektiveStaerke.ToString(), 16,
                    s.AufDemPlatz ? StaerkeFarbeSpieler(s.Staerke, s.EffektiveStaerke) : FmTheme.TextGedimmt);
            staerke.HorizontalAlignment = HorizontalAlignment.Right;
            staerke.MouseFilter = MouseFilterEnum.Pass;
            staerke.CustomMinimumSize = new Vector2(s.Geschaetzt ? 46 : 24, 0);
            staerke.TooltipText = s.AufDemPlatz || s.Geschaetzt
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
                CustomMinimumSize = new Vector2(46, 6),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            frische.AddThemeStyleboxOverride("fill",
                FmTheme.KonditionsBalken(FmTheme.FrischeFarbe(s.Kondition)));
            frische.AddThemeStyleboxOverride("background", FmTheme.KonditionsBalken(FmTheme.Border));
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

    /// <summary>Öffnet den Wechseldialog - auch für den Vorschau-Modus.</summary>
    internal void OeffneWechsel() => OnWechselnPressed();

    internal void Ueberspringe() => OnAktionPressed();

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

    // ── Umstellen ────────────────────────────────────────────────────────────

    /// <summary>
    /// Öffnet den Umstell-Dialog. Die Uhr steht, solange er offen ist; anders als ein Wechsel
    /// kostet Umstellen nichts und ist jederzeit erlaubt.
    /// </summary>
    internal async System.Threading.Tasks.Task OeffneTaktik(string? vorlage = null)
    {
        if (_abgepfiffen || _spiel == null) return;
        _pausiert = true;
        var standTask    = ApiClient.GetAsync<LiveTaktik>("spielfeld/taktik");
        var vorlagenTask = ApiClient.GetAsync<List<FormationsVorlage>>("aufstellung/vorlagen");
        var stand    = await standTask;
        var vorlagen = await vorlagenTask ?? new List<FormationsVorlage>();
        if (!Lebt) return;
        if (stand == null)
        {
            SchreibeZeile(_spiel.Minute, "Umstellen gerade nicht möglich.", FmTheme.Danger);
            if (!_abgepfiffen) _pausiert = false;
            return;
        }
        LiveTaktikDialog.Zeige(this, stand, vorlagen,
            beiBestaetigung: async neu => await StelleUm(neu),
            beiAbbruch: () => { if (!_abgepfiffen) _pausiert = false; });
        if (vorlage != null)
            FindeDialog()?.VorschauVorlage(vorlage);
    }

    private LiveTaktikDialog? FindeDialog()
    {
        foreach (var kind in (GetTree().CurrentScene ?? GetTree().Root).GetChildren())
            if (kind is LiveTaktikDialog d) return d;
        return null;
    }

    private async System.Threading.Tasks.Task StelleUm(LiveTaktik neu)
    {
        var ergebnis = await ApiClient.PostAsync<LiveTaktik, LiveTaktik>("spielfeld/taktik", neu);
        if (!Lebt) return;
        if (ergebnis == null)
            SchreibeZeile(_spiel?.Minute ?? 0, "Die Umstellung kam nicht an.", FmTheme.Danger);
        else
            SchreibeZeile(_spiel?.Minute ?? 0, $"Umgestellt: {ergebnis.Formation}.", FmTheme.TextSecondary);
        var zustand = await ApiClient.GetAsync<LiveSpiel>("spielfeld/stand");
        if (!Lebt) return;
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
        _taktikButton.Disabled = true;

        var zustand = await ApiClient.PostAsync<object, LiveSpiel>("spielfeld/abpfiff", new { });
        if (!Lebt) return;

        if (zustand != null)
        {
            Aktualisiere(zustand);
            TrageRestNach(zustand);
        }

        SchreibeZeile(Spielminuten,
            $"Abpfiff  –  Endstand {_spiel?.HeimTore} : {_spiel?.GastTore}",
            FmTheme.TextPrimary, Symbol.Art.Abpfiff);
        _uhr.Value = Spielminuten;
        _uhr.AddThemeStyleboxOverride("fill", FmTheme.KonditionsBalken(FmTheme.Accent));
        _minuteLabel.Text = $"{Spielminuten}'";
        _halbzeitLabel.Text = "Abpfiff";
        _pausenschild.Visible = false;

        // Der restliche Spieltag läuft im Hintergrund - erst danach sind Tabelle und
        // Statistiken stimmig, deshalb wird bis dahin gewartet.
        _spieltagLaeuft = true;
        _wechselButton.Visible = false;
        _taktikButton.Visible = false;
        _aktionButton.Disabled = true;
        _aktionButton.Text = "Restlicher Spieltag …";
        await WarteAufSpieltag();
        if (!Lebt) return;
        _spieltagLaeuft = false;

        await ZeigeZusammenfassung();
    }

    /// <summary>
    /// Nach dem Überspringen: Den Rest der Partie hat der Server in einem Zug gerechnet, das
    /// Spielfeld hat ihn nie gezeigt. Tore und Karten daraus kommen in den Verlauf, die
    /// Anzeigetafel springt auf den Endstand.
    ///
    /// <para>Vorher blieb die Tafel auf dem Stand beim Klick stehen, und im Verlauf fehlte alles
    /// danach - es sah aus, als sei die Partie dort abgebrochen und der Zwischenstand gewertet
    /// worden, obwohl Tabelle und Bericht das ganze Spiel enthielten.</para>
    /// </summary>
    private void TrageRestNach(LiveSpiel ende)
    {
        int tore = 0, karten = 0;
        bool angekuendigt = false;
        foreach (var e in ende.Ereignisse)
        {
            bool tor = e.Typ == "TOR";
            bool karte = e.Typ is "GELBE_KARTE" or "GELB_ROTE_KARTE" or "ROTE_KARTE";
            // Was der Verlauf schon zeigt, steht in derselben Reihenfolge vorn in der Liste.
            if (tor && ++tore <= _geschriebeneTore) continue;
            if (karte && ++karten <= _geschriebeneKarten) continue;
            if (!tor && !karte) continue;

            if (!angekuendigt)
            {
                SchreibeZeile(_gerechneteMinute, "Übersprungen - der Rest der Partie im Schnelldurchlauf",
                    FmTheme.TextSecondary);
                angekuendigt = true;
            }
            bool eigenes = e.VereinId == ende.EigenerVereinId;
            var (farbe, zeichen) = e.Typ switch
            {
                "TOR"         => (eigenes ? FmTheme.Success : FmTheme.Danger, Symbol.Art.Ball),
                "GELBE_KARTE" => (FmTheme.Gold, Symbol.Art.Gelb),
                "ROTE_KARTE"  => (FmTheme.Danger, Symbol.Art.Rot),
                _             => (FmTheme.Danger, Symbol.Art.GelbRot),
            };
            SchreibeZeile(e.Minute, e.Beschreibung ?? "", farbe, zeichen);
        }

        _feldHeimTore = ende.HeimTore;
        _feldGastTore = ende.GastTore;
        _ergebnisLabel.Text = $"{ende.HeimTore} : {ende.GastTore}";
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

        inhalt.AddChild(FmTheme.MakeTitel("Spielbericht", 30));

        var tafel = new StatistikTafel { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        inhalt.AddChild(tafel);
        tafel.Zeige(bericht.HeimStatistik, bericht.GastStatistik);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        inhalt.AddChild(sep);

        inhalt.AddChild(FmTheme.MakeAbschnitt("Noten", 22));

        var noten = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        noten.AddThemeConstantOverride("separation", 18);
        inhalt.AddChild(noten);

        noten.AddChild(Notenliste.Spalte(bericht.HeimVerein ?? "Heim", bericht.HeimAufstellung,
            bericht.HeimVereinId == _spiel?.EigenerVereinId));
        noten.AddChild(Notenliste.Spalte(bericht.GastVerein ?? "Gast", bericht.GastAufstellung,
            bericht.GastVereinId == _spiel?.EigenerVereinId));
    }

    // ── Protokoll ────────────────────────────────────────────────────────────

    private void SchreibeZeile(int minute, string text, Color farbe, Symbol.Art? zeichen = null)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);

        var min = FmTheme.MakeZahl($"{minute}'", 15, FmTheme.TextSecondary);
        min.HorizontalAlignment = HorizontalAlignment.Right;
        min.CustomMinimumSize = new Vector2(40, 0);
        zeile.AddChild(min);

        // Das Zeichen hält seinen Platz auch ohne Ereignis, damit die Texte untereinander stehen.
        zeile.AddChild(zeichen.HasValue
            ? Symbol.Neu(zeichen.Value, FmTheme.TextPrimary, 18)
            : new Control { CustomMinimumSize = new Vector2(18, 18) });

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
