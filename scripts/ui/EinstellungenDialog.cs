#nullable enable
using Godot;
using System;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Client-Einstellungen (Fenstermodus, Gehaltsanzeige) - rein lokal auf diesem Rechner, nicht Teil
/// des Spielstands. Über das Zahnrad in der Menüzeile und im Startbildschirm erreichbar.
///
/// <para>Jede Einstellung ist ein Segmentschalter: Es sind nur zwei, drei Möglichkeiten, und so
/// steht die gewählte sichtbar neben den anderen. Änderungen gelten sofort.</para>
/// </summary>
public partial class EinstellungenDialog : Overlaykarte
{
    private Action? _aufSchliessen;

    public EinstellungenDialog() : base("Einstellungen", 460) { }

    /// <param name="aufSchliessen">Wird beim Schließen aufgerufen - üblicherweise um die aktuelle
    /// Ansicht neu zu laden, damit eine geänderte Gehaltsanzeige sofort sichtbar wird.</param>
    public static void Zeige(Node caller, Action? aufSchliessen = null)
    {
        Oeffne(caller, new EinstellungenDialog { _aufSchliessen = aufSchliessen }, einzeln: "");
    }

    protected override void BaueInhalt(VBoxContainer inhalt)
    {
        SetzeUntertitel("Gelten nur auf diesem Rechner, nicht im Spielstand.");

        var fenster = Segmentschalter.Neu(new[] { "Fenster", "Vollbild" },
            ClientEinstellungen.GetBool("vollbild", false) ? 1 : 0, new Vector2(110, 34));
        fenster.Gewaehlt += i => AnwendenFenstermodus(i == 1);
        inhalt.AddChild(Einstellung("Anzeige", "Wie das Spiel auf dem Bildschirm steht.", fenster));

        var gehalt = Segmentschalter.Neu(new[] { "Woche", "Monat", "Jahr" },
            (int)Gehaltsformat.Periode, new Vector2(90, 34));
        gehalt.Gewaehlt += i => Gehaltsformat.Periode = (GehaltsPeriode)i;
        inhalt.AddChild(Einstellung("Gehalt anzeigen je", "Gilt für Kader, Verträge und Finanzen.", gehalt));
    }

    protected override void BeimSchliessen() => _aufSchliessen?.Invoke();

    /// <summary>Beschriftung mit Erklärung darunter, der Schalter darunter.</summary>
    private static Control Einstellung(string titel, string erklaerung, Control schalter)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(FmTheme.MakeAbschnitt(titel, 18));
        box.AddChild(FmTheme.MakeLabel(erklaerung, 12, FmTheme.TextSecondary));

        var abstand = new MarginContainer();
        FmTheme.SetMargin(abstand, 0, 0, 6, 4);
        abstand.AddChild(schalter);
        box.AddChild(abstand);
        return box;
    }

    /// <summary>Setzt den Fenstermodus sofort um und merkt ihn für den nächsten Start vor.</summary>
    private static void AnwendenFenstermodus(bool vollbild)
    {
        ClientEinstellungen.SetBool("vollbild", vollbild);
        DisplayServer.WindowSetMode(vollbild
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }
}
