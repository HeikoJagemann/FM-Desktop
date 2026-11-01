#nullable enable
using Godot;
using System;
using System.IO;
using System.Collections.Generic;
using FMDesktop.Api;

namespace FMDesktop.UI;

/// <summary>
/// Was direkt nach dem Klick auf das Symbol erscheint, während im Hintergrund Datenbank und
/// Backend hochfahren.
///
/// <para>Der Startbildschirm kommt erst, wenn das Backend antwortet - vorher hätte er nichts zu
/// zeigen als Fehlermeldungen. Das Startskript meldet währenddessen über eine Statusdatei, wo es
/// gerade steht; die Leiste zeigt also echte Schritte und keine gefällige Animation.</para>
///
/// <para>Ohne Statusdatei - etwa beim Start aus dem Godot-Editor - läuft der Bildschirm trotzdem:
/// Dann zählt allein, ob das Backend erreichbar ist.</para>
/// </summary>
public partial class LadeBildschirm : Control
{
    private const string SceneStart = "res://scenes/StartScreen.tscn";

    /// Wie oft geprüft wird, ob das Backend schon antwortet.
    private const double PruefIntervall = 0.5;

    /// Danach wird aufgegeben - Maven muss beim ersten Start ggf. erst Abhängigkeiten auflösen.
    private const double ZeitlimitSekunden = 180;

    /// <summary>
    /// Ohne Statusdatei schätzt die Leiste über die Zeit. Sie bleibt unter diesem Wert, damit sie
    /// nie "fertig" behauptet, bevor das Backend tatsächlich antwortet.
    /// </summary>
    private const double SchaetzungMax = 90;
    private const double SchaetzungDauer = 45;

    private ProgressBar _balken = null!;
    private Label _statusLabel = null!;
    private Label _hinweisLabel = null!;
    private Button _beendenButton = null!;

    private Timer _timer = null!;
    private double _verstrichen;
    private bool _fertig;

    /// <summary>
    /// Sperrt weitere Prüfungen, solange eine Anfrage unterwegs ist.
    ///
    /// <para>Ohne sie laufen mehrere Ticks gleichzeitig: Der Timer feuert alle halbe Sekunde
    /// weiter, während die Anfrage noch aussteht. Kommt das Backend hoch, bekommen zwei Ticks eine
    /// Antwort - der erste wechselt die Szene und gibt diesen Knoten frei, der zweite läuft danach
    /// in ein <c>GetTree() == null</c>.</para>
    /// </summary>
    private bool _pruefungLaeuft;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BaueUI();

        _timer = new Timer { WaitTime = PruefIntervall, OneShot = false };
        _timer.Timeout += OnTick;
        AddChild(_timer);
        _timer.Start();
    }

    // ── Aufbau ───────────────────────────────────────────────────────────────

    private void BaueUI()
    {
        AddChild(Hintergrundfeld.Neu());

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var vbox = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
        vbox.AddThemeConstantOverride("separation", 14);
        center.AddChild(vbox);

        var kicker = FmTheme.MakeLabel("DEINE TRAINERKARRIERE", 13, FmTheme.TextSecondary,
            HorizontalAlignment.Center);
        kicker.AddThemeFontOverride("font", FmTheme.SchriftUi(600));
        vbox.AddChild(kicker);
        var titel = FmTheme.MakeTitel("Fussball-Manager", 56);
        titel.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(titel);

        var abstand = new Control { CustomMinimumSize = new Vector2(0, 12) };
        vbox.AddChild(abstand);

        _balken = new ProgressBar
        {
            MinValue = 0, MaxValue = 100, Value = 0, ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 8),
        };
        _balken.AddThemeStyleboxOverride("fill", FmTheme.KonditionsBalken(FmTheme.Accent));
        _balken.AddThemeStyleboxOverride("background", FmTheme.KonditionsBalken(FmTheme.Border));
        vbox.AddChild(_balken);

        _statusLabel = FmTheme.MakeLabel("Startet …", 13, FmTheme.TextSecondary,
            HorizontalAlignment.Center);
        vbox.AddChild(_statusLabel);

        _hinweisLabel = FmTheme.MakeLabel("", 11, FmTheme.TextSecondary,
            HorizontalAlignment.Center);
        vbox.AddChild(_hinweisLabel);

        _beendenButton = new Button
        {
            Text = "Beenden",
            CustomMinimumSize = new Vector2(160, 44),
            Visible = false,
        };
        FmTheme.ApplySecondaryButton(_beendenButton);
        _beendenButton.AddThemeColorOverride("font_color", FmTheme.Danger);
        _beendenButton.Pressed += () => GameState.Instance.Beende();

        var buttonMitte = new CenterContainer();
        buttonMitte.AddChild(_beendenButton);
        vbox.AddChild(buttonMitte);
    }

    // ── Ablauf ───────────────────────────────────────────────────────────────

    private async void OnTick()
    {
        if (_fertig || _pruefungLaeuft) return;
        _verstrichen += PruefIntervall;

        var stand = LiesStatus();

        // Das Startskript meldet einen Abbruch mit negativem Prozentwert. Ohne dieses Zeichen
        // würde hier drei Minuten lang auf ein Backend gewartet, das nie kommt.
        if (stand is { Prozent: < 0 })
        {
            Abbrechen(stand.Value.Text);
            return;
        }
        ZeigeFortschritt(stand);

        // Der eigentliche Prüfstein: Antwortet das Backend, kann es losgehen. Die Statusdatei ist
        // nur Beiwerk für die Anzeige - sie könnte veraltet sein oder ganz fehlen.
        List<string>? schemas;
        _pruefungLaeuft = true;
        try
        {
            schemas = await ApiClient.GetAsync<List<string>>("schemas");
        }
        finally
        {
            _pruefungLaeuft = false;
        }

        // Während der Anfrage kann alles Mögliche passiert sein - der Nutzer hat das Fenster
        // geschlossen, oder ein anderer Weg hat schon abgebrochen.
        if (_fertig || !IsInstanceValid(this)) return;

        if (schemas != null)
        {
            _fertig = true;
            _timer.Stop();
            _balken.Value = 100;
            _statusLabel.Text = "Bereit";

            // Ein Lidschlag, damit die volle Leiste nicht wegblitzt.
            var baum = GetTree();
            if (baum == null) return;
            await ToSignal(baum.CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);

            if (!IsInstanceValid(this)) return;
            baum = GetTree();
            if (baum == null) return;
            baum.ChangeSceneToFile(SceneStart);
            return;
        }

        if (_verstrichen >= ZeitlimitSekunden)
        {
            Abbrechen("Das Backend antwortet nicht.");
        }
    }

    private void Abbrechen(string grund)
    {
        _timer.Stop();
        _fertig = true;
        _statusLabel.Text = grund;
        _statusLabel.AddThemeColorOverride("font_color", FmTheme.Danger);
        _hinweisLabel.Text = "Protokoll: %LOCALAPPDATA%\\Fussballmanager\\backend.log";
        _beendenButton.Visible = true;
    }

    /// <summary>
    /// Bevorzugt die Meldung des Startskripts; ohne sie eine Schätzung über die Zeit, die unter
    /// 100 % bleibt.
    /// </summary>
    private void ZeigeFortschritt((double Prozent, string Text)? stand)
    {
        if (stand != null)
        {
            _balken.Value = stand.Value.Prozent;
            _statusLabel.Text = stand.Value.Text;
            return;
        }

        double anteil = Math.Min(1.0, _verstrichen / SchaetzungDauer);
        _balken.Value = anteil * SchaetzungMax;
        _statusLabel.Text = "Warte auf das Backend …";
    }

    /// <summary>
    /// Der Fortschritt des Startskripts, oder <c>null</c>, wenn es keinen gibt.
    ///
    /// <para>Format: "&lt;prozent&gt;|&lt;text&gt;", wobei ein negativer Prozentwert einen Abbruch
    /// meldet. Der Wert wird deshalb nicht nach unten begrenzt.</para>
    ///
    /// <para>Fehler werden geschluckt - die Datei wird nebenher geschrieben, ein halb
    /// geschriebener Stand ist beim nächsten Tick wieder heil.</para>
    /// </summary>
    private static (double Prozent, string Text)? LiesStatus()
    {
        try
        {
            string basis = OS.GetEnvironment("LOCALAPPDATA");
            if (string.IsNullOrEmpty(basis)) return null;

            string pfad = Path.Combine(basis, "Fussballmanager", "start-status.txt");
            if (!File.Exists(pfad)) return null;

            string[] teile = File.ReadAllText(pfad).Trim().Split('|');
            if (teile.Length < 2) return null;
            // Fest mit invarianter Kultur: Das Skript schreibt Zahlen ohne Rücksicht darauf, wie
            // dieser Rechner Dezimaltrenner setzt.
            if (!double.TryParse(teile[0], System.Globalization.NumberStyles.Any,
                                 System.Globalization.CultureInfo.InvariantCulture,
                                 out double prozent)) return null;

            return (Math.Min(prozent, 100), teile[1]);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
