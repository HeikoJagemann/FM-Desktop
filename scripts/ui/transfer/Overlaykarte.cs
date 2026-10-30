#nullable enable
using Godot;
using FMDesktop.UI;

namespace FMDesktop.UI.Transfer;

/// <summary>
/// Eine Karte über abgedunkelter Ansicht - Grundgerüst der Dialoge des Transfermarkts. Wie der
/// <see cref="EinstellungenDialog"/>: Escape oder ein Klick daneben schließt.
/// </summary>
public partial class Overlaykarte : Control
{
    private readonly string _titel;
    private Label _meldung = null!;

    public Overlaykarte() : this("") { }

    protected Overlaykarte(string titel)
    {
        _titel = titel;
    }

    /// <summary>Hängt die Karte über die aktuelle Szene.</summary>
    protected static void Oeffne(Node caller, Overlaykarte karte)
    {
        var scene = caller.GetTree().CurrentScene ?? caller.GetTree().Root;
        scene.AddChild(karte);
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        bg.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Schliessen();
        };
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(440, 0) };
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle(10));
        center.AddChild(panel);

        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 22);
        panel.AddChild(rand);

        var inhalt = new VBoxContainer();
        inhalt.AddThemeConstantOverride("separation", 12);
        rand.AddChild(inhalt);

        inhalt.AddChild(FmTheme.MakeTitel(_titel, 22));
        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        inhalt.AddChild(sep);

        BaueInhalt(inhalt);

        _meldung = FmTheme.MakeLabel("", 13, FmTheme.Danger);
        _meldung.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _meldung.CustomMinimumSize = new Vector2(400, 0);
        _meldung.Visible = false;
        inhalt.AddChild(_meldung);
    }

    /// <summary>Hier trägt die Unterklasse ihre Felder und Knöpfe ein.</summary>
    protected virtual void BaueInhalt(VBoxContainer inhalt) { }

    /// <summary>Zeigt eine Ablehnung in der Karte - sie bleibt offen, damit man korrigieren kann.</summary>
    protected void ZeigeFehler(string text)
    {
        _meldung.Text = text;
        _meldung.Visible = true;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Schliessen();
            GetViewport().SetInputAsHandled();
        }
    }

    protected void Schliessen()
    {
        if (IsInstanceValid(this)) QueueFree();
    }

    /// <summary>Zeile aus Beschriftung und Wert.</summary>
    protected static Control Zeile(string beschriftung, Control wert)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 12);
        var label = FmTheme.MakeLabel(beschriftung, 13, FmTheme.TextSecondary);
        label.CustomMinimumSize = new Vector2(150, 0);
        zeile.AddChild(label);
        wert.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        zeile.AddChild(wert);
        return zeile;
    }

    /// <summary>Abbrechen und eine Hauptaktion, rechtsbündig.</summary>
    protected HBoxContainer Knopfleiste(string aktion, System.Action gedrueckt)
    {
        var leiste = new HBoxContainer();
        leiste.AddThemeConstantOverride("separation", 10);
        leiste.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var abbrechen = new Button { Text = "Abbrechen" };
        FmTheme.ApplySecondaryButton(abbrechen);
        abbrechen.Pressed += Schliessen;
        leiste.AddChild(abbrechen);

        var los = new Button { Text = aktion };
        FmTheme.ApplyPrimaryButton(los);
        los.Pressed += gedrueckt;
        leiste.AddChild(los);
        return leiste;
    }
}
