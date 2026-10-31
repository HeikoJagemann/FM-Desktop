#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace FMDesktop.UI;

/// <summary>
/// Eine Karte über abgedunkelter Ansicht - das Grundgerüst aller Dialoge: Titel in der
/// Anzeigeschrift, darunter auf Wunsch eine Zeile Zusammenhang, rechts ein gezeichnetes Kreuz.
/// Escape, das Kreuz oder ein Klick daneben schließen.
///
/// <para>Vorher baute jeder Dialog sein Gerüst selbst - mit eigener Breite, eigenem Rand,
/// eigenem Schließen-Knopf als "✕"-Zeichen und eigener Buchführung, ob er schon offen ist.</para>
/// </summary>
public partial class Overlaykarte : Control
{
    /// <summary>Je Dialogart die offene Karte und wofür sie offen ist - siehe <see cref="Oeffne"/>.</summary>
    private static readonly Dictionary<Type, (Overlaykarte Karte, string Schluessel)> _offen = new();

    private readonly string _titel;
    private readonly float _breite;
    private string _untertitelText = "";
    private Label? _untertitel;
    private Label _meldung = null!;
    private bool _geschlossen;

    /// <summary>
    /// Ob ein Klick neben die Karte sie schließt. Aus bei Dialogen, in denen Schließen eine
    /// Zusammenstellung verwirft - dort soll das nur bewusst geschehen.
    /// </summary>
    protected bool SchliesstBeiKlickDaneben = true;

    public Overlaykarte() : this("") { }

    protected Overlaykarte(string titel, float breite = 440)
    {
        _titel = titel;
        _breite = breite;
    }

    /// <summary>
    /// Hängt die Karte über die aktuelle Szene.
    /// </summary>
    /// <param name="einzeln">
    /// Gesetzt, gibt es von dieser Dialogart höchstens eine Karte: Ist eine für denselben
    /// Schlüssel schon offen, bleibt es bei ihr; eine für einen anderen wird ersetzt. So öffnet
    /// ein zweiter Klick auf denselben Spieler kein zweites Fenster.
    /// </param>
    protected static void Oeffne(Node caller, Overlaykarte karte, string? einzeln = null)
    {
        if (einzeln != null)
        {
            if (_offen.TryGetValue(karte.GetType(), out var alt) && IsInstanceValid(alt.Karte)
                && !alt.Karte.IsQueuedForDeletion())
            {
                if (alt.Schluessel == einzeln)
                {
                    karte.Free();
                    return;
                }
                alt.Karte.Schliessen();
            }
            _offen[karte.GetType()] = (karte, einzeln);
        }

        var scene = caller.GetTree().CurrentScene ?? caller.GetTree().Root;
        scene.AddChild(karte);
    }

    public override void _ExitTree()
    {
        if (_offen.TryGetValue(GetType(), out var eintrag) && eintrag.Karte == this)
            _offen.Remove(GetType());
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect { Color = new Color(0.02f, 0.035f, 0.03f, 0.72f) };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        bg.GuiInput += ev =>
        {
            if (SchliesstBeiKlickDaneben
                && ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                Schliessen();
        };
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(_breite, 0) };
        panel.AddThemeStyleboxOverride("panel", FmTheme.Flaeche(FmTheme.BgPanel, 14, FmTheme.BorderStark));
        center.AddChild(panel);

        var rand = new MarginContainer();
        FmTheme.SetMargin(rand, 24, 24, 20, 22);
        panel.AddChild(rand);

        var inhalt = new VBoxContainer();
        inhalt.AddThemeConstantOverride("separation", 14);
        rand.AddChild(inhalt);

        inhalt.AddChild(BaueKopf());
        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        inhalt.AddChild(sep);

        BaueInhalt(inhalt);

        _meldung = FmTheme.MakeLabel("", 13, FmTheme.Danger);
        _meldung.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _meldung.CustomMinimumSize = new Vector2(_breite - 48, 0);
        _meldung.Visible = false;
        inhalt.AddChild(_meldung);
    }

    private Control BaueKopf()
    {
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 12);

        var titel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titel.AddThemeConstantOverride("separation", 0);
        titel.AddChild(FmTheme.MakeTitel(_titel, 26));
        _untertitel = FmTheme.MakeLabel(_untertitelText, 13, FmTheme.TextSecondary);
        _untertitel.Visible = _untertitelText != "";
        titel.AddChild(_untertitel);
        kopf.AddChild(titel);

        kopf.AddChild(SchliessenKnopf());
        return kopf;
    }

    /// <summary>Das Kreuz oben rechts - ruhig, erst beim Überfahren hinterlegt.</summary>
    private Button SchliessenKnopf()
    {
        var knopf = new Button
        {
            CustomMinimumSize = new Vector2(34, 34),
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            FocusMode = FocusModeEnum.None,
            TooltipText = "Schließen (Esc)",
        };
        knopf.AddThemeStyleboxOverride("normal", FmTheme.Flaeche(new Color(0, 0, 0, 0), 8));
        knopf.AddThemeStyleboxOverride("hover", FmTheme.Flaeche(FmTheme.BgHover, 8));
        knopf.AddThemeStyleboxOverride("pressed", FmTheme.Flaeche(FmTheme.Border, 8));

        var kreuz = Symbol.Neu(Symbol.Art.Kreuz, FmTheme.TextSecondary, 16);
        kreuz.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        knopf.AddChild(kreuz);
        knopf.MouseEntered += () => kreuz.Farbe = FmTheme.TextPrimary;
        knopf.MouseExited += () => kreuz.Farbe = FmTheme.TextSecondary;

        knopf.Pressed += Schliessen;
        return knopf;
    }

    /// <summary>Hier trägt die Unterklasse ihre Felder und Knöpfe ein.</summary>
    protected virtual void BaueInhalt(VBoxContainer inhalt) { }

    /// <summary>Die Zeile unter dem Titel - auch nachträglich, wenn die Daten erst kommen.</summary>
    protected void SetzeUntertitel(string text)
    {
        _untertitelText = text;
        if (_untertitel == null) return;
        _untertitel.Text = text;
        _untertitel.Visible = text != "";
    }

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

    /// <summary>Schließt die Karte; <see cref="BeimSchliessen"/> läuft genau einmal.</summary>
    protected void Schliessen()
    {
        if (_geschlossen || !IsInstanceValid(this)) return;
        _geschlossen = true;
        BeimSchliessen();
        QueueFree();
    }

    /// <summary>Was beim Schließen geschehen soll, gleich auf welchem Weg.</summary>
    protected virtual void BeimSchliessen() { }

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
