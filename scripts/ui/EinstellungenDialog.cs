#nullable enable
using Godot;
using System;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Client-Einstellungen (Fenstermodus, Gehaltsanzeige) - rein lokal auf diesem Rechner, nicht Teil
/// des Spielstands. Über das Werkzeug-Icon im Header jeder Spielansicht erreichbar.
/// </summary>
public partial class EinstellungenDialog : Control
{
    private static EinstellungenDialog? _aktuelleInstanz;

    private Action? _aufSchliessen;

    /// <param name="aufSchliessen">Wird beim Schließen aufgerufen - üblicherweise um die aktuelle
    /// Ansicht neu zu laden, damit eine geänderte Gehaltsanzeige sofort sichtbar wird.</param>
    public static void Zeige(Node caller, Action? aufSchliessen = null)
    {
        if (_aktuelleInstanz != null && IsInstanceValid(_aktuelleInstanz)) return; // schon offen

        var overlay = new EinstellungenDialog { _aufSchliessen = aufSchliessen };
        _aktuelleInstanz = overlay;
        var scene = caller.GetTree().CurrentScene ?? caller.GetTree().Root;
        scene.AddChild(overlay);
    }

    public override void _ExitTree()
    {
        if (_aktuelleInstanz == this) _aktuelleInstanz = null;
    }

    public override void _Ready()
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

        center.AddChild(BuildCard());
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Schliessen();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnHintergrundInput(InputEvent ev)
    {
        if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            Schliessen();
    }

    private void Schliessen()
    {
        _aufSchliessen?.Invoke();
        if (IsInstanceValid(this)) QueueFree();
    }

    private Control BuildCard()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(380, 0) };
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle(10));

        var outerMargin = new MarginContainer();
        FmTheme.SetMargin(outerMargin, 22);
        panel.AddChild(outerMargin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 16);
        outerMargin.AddChild(root);

        root.AddChild(BuildHeader());

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        root.AddChild(sep);

        root.AddChild(BuildFenstermodusZeile());
        root.AddChild(BuildGehaltszeile());

        return panel;
    }

    private Control BuildHeader()
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);

        var titel = FmTheme.MakeTitel("Einstellungen", 24);
        titel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.AddChild(titel);

        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(32, 32) };
        FmTheme.ApplyButton(close, FmTheme.BgPanel);
        close.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
        close.Pressed += Schliessen;
        hbox.AddChild(close);

        return hbox;
    }

    private static Control BuildFenstermodusZeile()
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(FmTheme.MakeLabel("Anzeige", 12, FmTheme.TextSecondary));

        var auswahl = new OptionButton { CustomMinimumSize = new Vector2(220, 0) };
        auswahl.AddItem("Fenster", 0);
        auswahl.AddItem("Vollbild", 1);
        auswahl.Selected = ClientEinstellungen.GetBool("vollbild", false) ? 1 : 0;
        auswahl.ItemSelected += id => AnwendenFenstermodus(id == 1);
        vbox.AddChild(auswahl);

        return vbox;
    }

    /// <summary>Setzt den Fenstermodus sofort um und merkt ihn für den nächsten Start vor.</summary>
    private static void AnwendenFenstermodus(bool vollbild)
    {
        ClientEinstellungen.SetBool("vollbild", vollbild);
        DisplayServer.WindowSetMode(vollbild
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }

    private static Control BuildGehaltszeile()
    {
        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(FmTheme.MakeLabel("Gehalt anzeigen als", 12, FmTheme.TextSecondary));

        var auswahl = new OptionButton { CustomMinimumSize = new Vector2(220, 0) };
        auswahl.AddItem("Wochengehalt", (int)GehaltsPeriode.Woche);
        auswahl.AddItem("Monatsgehalt", (int)GehaltsPeriode.Monat);
        auswahl.AddItem("Jahresgehalt", (int)GehaltsPeriode.Jahr);
        auswahl.Selected = (int)Gehaltsformat.Periode;
        auswahl.ItemSelected += id => Gehaltsformat.Periode = (GehaltsPeriode)id;
        vbox.AddChild(auswahl);

        return vbox;
    }
}
