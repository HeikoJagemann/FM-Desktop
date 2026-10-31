#nullable enable
using Godot;
using System.Collections.Generic;
using System.Globalization;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Die Noten einer Mannschaft als Karte: Wappen und Name, darunter je Spieler Position, Name,
/// Tore, Vorlagen und Karten als Zeichen, Einsatzzeit und Note.
///
/// <para>Gebraucht nach dem Abpfiff in der Spielansicht und im Spielbericht eines beliebigen
/// Spiels.</para>
/// </summary>
public static class Notenliste
{
    private const int Spielminuten = 90;

    public static Control Spalte(string verein, List<AufstellungsSpieler> spieler, bool eigener)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle(14));

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 16, 12);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vbox);

        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 10);
        kopf.AddChild(Wappen.Neu(verein, 32, eigener ? FmTheme.Accent : FmTheme.BorderStark));
        var name = FmTheme.MakeAbschnitt(verein, 18);
        name.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        kopf.AddChild(name);
        vbox.AddChild(kopf);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        vbox.AddChild(sep);

        foreach (var s in spieler)
        {
            vbox.AddChild(Zeile(s));
        }
        return panel;
    }

    private static Control Zeile(AufstellungsSpieler s)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 8);

        var slot = FmTheme.MakeLabel(s.Slot, 11, FmTheme.TextSecondary);
        slot.CustomMinimumSize = new Vector2(44, 0);
        zeile.AddChild(slot);

        var name = FmTheme.MakeLabel(s.Name, 12,
            s.Startelf ? FmTheme.TextPrimary : FmTheme.TextSecondary);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        zeile.AddChild(name);

        // Tore, Vorlagen und Karten als Symbole - eine Zahlenspalte je Kategorie wäre in einer
        // Notenübersicht mehr Tabelle als Information.
        var marken = Marken(s);
        marken.CustomMinimumSize = new Vector2(70, 0);
        zeile.AddChild(marken);

        var einsatz = FmTheme.MakeLabel(EinsatzText(s), 11, FmTheme.TextSecondary,
            HorizontalAlignment.Right);
        einsatz.CustomMinimumSize = new Vector2(62, 0);
        einsatz.TooltipText = "Einsatzzeit";
        zeile.AddChild(einsatz);

        var note = FmTheme.MakeZahl(NoteText(s.Note), 16, NotenFarbe(s.Note));
        note.HorizontalAlignment = HorizontalAlignment.Right;
        note.CustomMinimumSize = new Vector2(38, 0);
        note.MouseFilter = Control.MouseFilterEnum.Pass;
        note.TooltipText = "Note von 1 bis 10 - höher ist besser, 6 ist ein durchschnittlicher Auftritt.";
        zeile.AddChild(note);

        return zeile;
    }

    /// <summary>Tore, Vorlagen und Karten als kleine Zeichen - gezeichnet, nicht als Emoji.</summary>
    private static Control Marken(AufstellungsSpieler s)
    {
        var reihe = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        reihe.AddThemeConstantOverride("separation", 2);
        for (int i = 0; i < s.Tore; i++)        reihe.AddChild(Symbol.Neu(Symbol.Art.Ball, FmTheme.TextPrimary, 16));
        for (int i = 0; i < s.Vorlagen; i++)
        {
            var vorlage = FmTheme.MakeLabel("V", 11, FmTheme.TextSecondary);
            vorlage.AddThemeFontOverride("font", FmTheme.SchriftUi(700));
            reihe.AddChild(vorlage);
        }
        for (int i = 0; i < s.GelbeKarten; i++) reihe.AddChild(Symbol.Neu(Symbol.Art.Gelb, FmTheme.TextPrimary, 16));
        for (int i = 0; i < s.RoteKarten; i++)  reihe.AddChild(Symbol.Neu(Symbol.Art.Rot, FmTheme.TextPrimary, 16));

        var teile = new List<string>();
        if (s.Tore > 0) teile.Add(s.Tore == 1 ? "1 Tor" : $"{s.Tore} Tore");
        if (s.Vorlagen > 0) teile.Add(s.Vorlagen == 1 ? "1 Vorlage" : $"{s.Vorlagen} Vorlagen");
        if (s.GelbeKarten > 0) teile.Add("Gelbe Karte");
        if (s.RoteKarten > 0) teile.Add("Rote Karte");
        reihe.TooltipText = string.Join(", ", teile);
        return reihe;
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
}
