#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace FMDesktop.UI;

/// <summary>
/// Mehrere Möglichkeiten nebeneinander in einer Fassung, die gewählte in Bernstein - für
/// Formation, Spieltempo und Einstellungen. Alle Möglichkeiten sind auf einen Blick zu sehen,
/// anders als in einer Ausklappliste.
/// </summary>
public partial class Segmentschalter : PanelContainer
{
    private readonly List<Button> _knoepfe = new();
    private int _auswahl;
    private int _schriftgroesse;
    private bool _gross;

    /// <summary>Der Nutzer hat eine andere Möglichkeit gewählt.</summary>
    public event Action<int>? Gewaehlt;

    /// <param name="knopfgroesse">Mindestgröße je Knopf; ab 34 Pixel Höhe wird die Fassung etwas großzügiger.</param>
    /// <param name="schriftgroesse">Größe der Anzeigeschrift.</param>
    public static Segmentschalter Neu(IReadOnlyList<string> texte, int auswahl, Vector2 knopfgroesse,
                                      int schriftgroesse = 16)
    {
        var schalter = new Segmentschalter
        {
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            _schriftgroesse = schriftgroesse,
            _gross = knopfgroesse.Y >= 34,
        };
        var fassung = FmTheme.Flaeche(FmTheme.BgPanel, schalter._gross ? 12 : 10, FmTheme.Border);
        fassung.SetContentMarginAll(schalter._gross ? 4 : 3);
        schalter.AddThemeStyleboxOverride("panel", fassung);

        var reihe = new HBoxContainer();
        reihe.AddThemeConstantOverride("separation", schalter._gross ? 4 : 3);
        schalter.AddChild(reihe);

        for (int i = 0; i < texte.Count; i++)
        {
            var knopf = new Button
            {
                Text = texte[i],
                FocusMode = FocusModeEnum.None,
                CustomMinimumSize = knopfgroesse,
            };
            int index = i;
            knopf.Pressed += () =>
            {
                if (index == schalter._auswahl) return;
                schalter.Auswahl = index;
                schalter.Gewaehlt?.Invoke(index);
            };
            schalter._knoepfe.Add(knopf);
            reihe.AddChild(knopf);
        }

        schalter._auswahl = auswahl;
        schalter.Zeichne();
        return schalter;
    }

    /// <summary>Die gewählte Möglichkeit. Setzen zeichnet nur neu und meldet nichts.</summary>
    public int Auswahl
    {
        get => _auswahl;
        set
        {
            _auswahl = value;
            Zeichne();
        }
    }

    /// <summary>Ein einzelner Knopf - etwa für seinen Tooltip.</summary>
    public Button Knopf(int index) => _knoepfe[index];

    private void Zeichne()
    {
        int radius = _gross ? 8 : 7;
        for (int i = 0; i < _knoepfe.Count; i++)
        {
            var knopf = _knoepfe[i];
            bool aktiv = i == _auswahl;
            var stil = FmTheme.Flaeche(aktiv ? FmTheme.Accent : new Color(0, 0, 0, 0), radius);
            var schwebend = FmTheme.Flaeche(aktiv ? FmTheme.Accent : FmTheme.BgHover, radius);
            foreach (var zustand in new[] { "normal", "pressed", "focus" })
                knopf.AddThemeStyleboxOverride(zustand, stil);
            knopf.AddThemeStyleboxOverride("hover", schwebend);
            var farbe = aktiv ? FmTheme.AccentText : FmTheme.TextNav;
            knopf.AddThemeColorOverride("font_color", farbe);
            knopf.AddThemeColorOverride("font_pressed_color", farbe);
            knopf.AddThemeColorOverride("font_hover_color", aktiv ? FmTheme.AccentText : FmTheme.TextPrimary);
            knopf.AddThemeFontOverride("font", FmTheme.SchriftAnzeige);
            knopf.AddThemeFontSizeOverride("font_size", _schriftgroesse);
        }
    }
}
