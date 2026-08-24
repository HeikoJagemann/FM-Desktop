#nullable enable
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>Modales Overlay mit allen Stammdaten, Fähigkeiten und Statistiken eines Spielers.</summary>
public partial class SpielerDetailOverlay : Control
{
    private static SpielerDetailOverlay? _aktuelleInstanz;

    /// Über wie viele Spieltage die Veränderung neben den Fähigkeiten zusammengefasst wird.
    private const int Spieltagsfenster = 10;

    private Spieler _s = null!;

    /// Entwicklung der letzten Spieltage; wird vor dem Aufbau der Karte nachgeladen.
    private SpielerVerlauf? _verlauf;

    public static void Zeige(Node caller, Spieler spieler)
    {
        if (_aktuelleInstanz != null && IsInstanceValid(_aktuelleInstanz))
        {
            if (_aktuelleInstanz._s.Id == spieler.Id) return; // schon offen für diesen Spieler
            _aktuelleInstanz.QueueFree();
        }

        var overlay = new SpielerDetailOverlay { _s = spieler };
        _aktuelleInstanz = overlay;
        var scene = caller.GetTree().CurrentScene ?? caller.GetTree().Root;
        scene.AddChild(overlay);
    }

    public override void _ExitTree()
    {
        if (_aktuelleInstanz == this) _aktuelleInstanz = null;
    }

    public override async void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        bg.GuiInput += OnHintergrundInput;
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        // Der Verlauf wird nachgeladen, bevor die Karte entsteht - der abgedunkelte Hintergrund
        // steht dabei schon, das Fenster erscheint also nicht spuerbar spaeter.
        _verlauf = await ApiClient.GetAsync<SpielerVerlauf>(
            $"training/spieler/{_s.Id}/verlauf?spieltage={Spieltagsfenster}");

        if (!IsInstanceValid(this) || !IsInstanceValid(center)) return;
        center.AddChild(BuildCard());
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            QueueFree();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnHintergrundInput(InputEvent ev)
    {
        if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            QueueFree();
    }

    private Control BuildCard()
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(620, 0),
        };
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle(10));

        var outerMargin = new MarginContainer();
        FmTheme.SetMargin(outerMargin, 22);
        panel.AddChild(outerMargin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        outerMargin.AddChild(root);

        root.AddChild(BuildHeader());

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        root.AddChild(sep);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 460),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddChild(scroll);

        var inhalt = new VBoxContainer();
        inhalt.AddThemeConstantOverride("separation", 14);
        inhalt.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(inhalt);

        inhalt.AddChild(BuildStammdatenCard());
        inhalt.AddChild(BuildStatistikenCard());
        inhalt.AddChild(BuildFaehigkeitenCard());
        if (_s.Position == "TW")
            inhalt.AddChild(BuildTorwartCard());

        return panel;
    }

    private Control BuildHeader()
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);

        var titelBox = new VBoxContainer();
        titelBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titelBox.AddChild(FmTheme.MakeLabel(_s.Name, 20, FmTheme.TextPrimary));
        titelBox.AddChild(FmTheme.MakeLabel(
            $"{_s.Position}  •  {KaderLabel(_s.Kader)}  •  {_s.Nationalitaet}  •  {_s.Alter} Jahre",
            13, FmTheme.TextSecondary));
        hbox.AddChild(titelBox);

        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(32, 32) };
        FmTheme.ApplyButton(close, FmTheme.BgPanel);
        close.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
        close.Pressed += QueueFree;
        hbox.AddChild(close);

        return hbox;
    }

    private static string KaderLabel(string kader) => kader switch
    {
        "JugendA" => "Jugend A",
        "JugendB" => "Jugend B",
        "JugendC" => "Jugend C",
        _         => kader,
    };

    private Control BuildStammdatenCard() => BaueCard("📋  Stammdaten", new VBoxContainer().Also(v =>
    {
        v.AddThemeConstantOverride("separation", 6);
        v.AddChild(InfoZeile("Geburtsdatum", _s.Geburtsdatum.ToString("dd.MM.yyyy")));
        v.AddChild(InfoZeile("Nationalität", _s.Nationalitaet));
        v.AddChild(InfoZeile("Marktwert", $"{_s.Wert:N0} €"));
        v.AddChild(InfoZeile("Kader", KaderLabel(_s.Kader)));
        v.AddChild(InfoZeile("Beste Positionen", _s.Top3PositionenText));
    }));

    private Control BuildStatistikenCard() => BaueCard("📊  Statistiken", new VBoxContainer().Also(v =>
    {
        v.AddThemeConstantOverride("separation", 6);
        v.AddChild(AttributZeile("Gesamtstärke", _s.BestPositionStaerke,
                                 differenz: _verlauf?.StaerkeDifferenz ?? 0));
        v.AddChild(AttributZeile("Talent", _s.Talent));
        // Das Potenzial wird bewusst nur als Spanne gezeigt - den exakten Wert liefert das
        // Backend nicht aus, kein Manager kennt das Maximum eines Spielers auf den Punkt.
        v.AddChild(InfoZeile("Potenzial (geschätzt)", $"{_s.PotenzialText}   {_s.PotenzialSterneText}"));
        v.AddChild(AttributZeile("Frische", _s.Frische));
        v.AddChild(InfoZeile("Spiele in dieser Saison", _s.SpieleInSaison.ToString()));
        if (_verlauf != null && _verlauf.BisSpieltag > 0)
        {
            v.AddChild(InfoZeile("Pfeile zeigen",
                $"Veränderung ab Spieltag {_verlauf.VonSpieltag}"));
        }
    }));

    private Control BuildFaehigkeitenCard() => BaueCard("⚙️  Fähigkeiten", new VBoxContainer().Also(v =>
    {
        v.AddThemeConstantOverride("separation", 6);
        v.AddChild(AttributZeile("Pass", _s.Pass, differenz: Diff("PASS")));
        v.AddChild(AttributZeile("Ballkontrolle", _s.Ballkontrolle, differenz: Diff("BALLKONTROLLE")));
        v.AddChild(AttributZeile("Schusstechnik", _s.Schusstechnik, differenz: Diff("SCHUSSTECHNIK")));
        v.AddChild(AttributZeile("Schussstärke", _s.Schussstaerke, differenz: Diff("SCHUSSSTAERKE")));
        v.AddChild(AttributZeile("Dribbling", _s.Dribbling, differenz: Diff("DRIBBLING")));
        v.AddChild(AttributZeile("Kopfball", _s.Kopfball, differenz: Diff("KOPFBALL")));
        v.AddChild(AttributZeile("Zweikampf", _s.Zweikampf, differenz: Diff("ZWEIKAMPF")));
        v.AddChild(AttributZeile("Stellungsspiel", _s.Stellungsspiel, differenz: Diff("STELLUNGSSPIEL")));
        v.AddChild(AttributZeile("Entscheidungen", _s.Entscheidungen, differenz: Diff("ENTSCHEIDUNGEN")));
        v.AddChild(AttributZeile("Führungsqualität", _s.Fuehrungsqualitaet, differenz: Diff("FUEHRUNGSQUALITAET")));
        v.AddChild(AttributZeile("Disziplin", _s.Disziplin, differenz: Diff("DISZIPLIN")));
        v.AddChild(AttributZeile("Linker Fuß", _s.LinkerFuss));
        v.AddChild(AttributZeile("Rechter Fuß", _s.RechterFuss));
        v.AddChild(AttributZeile("Schnelligkeit", _s.Schnelligkeit, differenz: Diff("SCHNELLIGKEIT")));
        v.AddChild(AttributZeile("Ausdauer", _s.Ausdauer, differenz: Diff("AUSDAUER")));
    }));

    private Control BuildTorwartCard() => BaueCard("🧤  Torwart", new VBoxContainer().Also(v =>
    {
        v.AddThemeConstantOverride("separation", 6);
        v.AddChild(AttributZeile("Talent (TW)", _s.TalentTW));
        v.AddChild(AttributZeile("Strafraumbeherrschung", _s.Strafraumbeherrschung, differenz: Diff("STRAFRAUMBEHERRSCHUNG")));
        v.AddChild(AttributZeile("Fangsicherheit", _s.Fangsicherheit, differenz: Diff("FANGSICHERHEIT")));
        v.AddChild(AttributZeile("Reflexe", _s.Reflexe, differenz: Diff("REFLEXE")));
    }));

    private static Control BaueCard(string titel, Control inhalt)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 14);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel(titel, 14, FmTheme.TextPrimary));
        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        vbox.AddChild(sep);
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

    /// <summary>Veränderung einer Fähigkeit über das Auswertungsfenster, oder 0.</summary>
    private int Diff(string attribut) => _verlauf?.DifferenzFuer(attribut) ?? 0;

    /// <param name="differenz">
    /// Veränderung über die letzten Spieltage. Ungleich 0 setzt Pfeil und Betrag hinter den Wert -
    /// so ist auf einen Blick erkennbar, woran der Spieler zuletzt gewachsen ist.
    /// </param>
    private static Control AttributZeile(string label, int wert, int max = 100, int differenz = 0)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);

        var l = FmTheme.MakeLabel(label, 13, FmTheme.TextSecondary);
        l.CustomMinimumSize = new Vector2(150, 0);
        row.AddChild(l);

        var bar = new ProgressBar
        {
            MinValue        = 0,
            MaxValue        = max,
            Value           = wert,
            ShowPercentage  = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize   = new Vector2(0, 14),
        };
        var farbe = FarbeFuerWert(wert);
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = farbe, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 });
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = FmTheme.BgToolbar, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 });
        row.AddChild(bar);

        var wertLabel = FmTheme.MakeLabel(wert.ToString(), 13, FmTheme.TextPrimary, HorizontalAlignment.Right);
        wertLabel.CustomMinimumSize = new Vector2(32, 0);
        row.AddChild(wertLabel);

        // Die Spalte bleibt auch ohne Veränderung stehen, damit die Werte darüber und darunter
        // nicht springen.
        var trend = FmTheme.MakeLabel(
            differenz == 0 ? "" : $"{(differenz > 0 ? "▲" : "▼")}{System.Math.Abs(differenz)}",
            12,
            differenz > 0 ? FmTheme.Success : differenz < 0 ? FmTheme.Danger : FmTheme.TextSecondary,
            HorizontalAlignment.Right);
        trend.CustomMinimumSize = new Vector2(34, 0);
        row.AddChild(trend);

        return row;
    }

    private static Color FarbeFuerWert(int wert) => wert switch
    {
        >= 80 => FmTheme.Gold,
        >= 65 => FmTheme.Success,
        >= 50 => FmTheme.TextPrimary,
        _     => FmTheme.TextSecondary,
    };
}

file static class NodeExtensions
{
    public static T Also<T>(this T node, System.Action<T> configure) where T : Node
    {
        configure(node);
        return node;
    }
}
