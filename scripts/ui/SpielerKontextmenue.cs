#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>Rechtsklick-Kontextmenü für Spielerzeilen (Kader, Jugend, ...).</summary>
public static class SpielerKontextmenue
{
    /// <summary>Wird nach einem Kaderwechsel gerufen, damit die Ansicht neu laden kann.</summary>
    public static event Action? KaderGeaendert;

    private static readonly (string Schluessel, string Anzeige)[] Kader =
    {
        ("Profi",   "Profikader"),
        ("Amateur", "Amateurkader"),
        ("JugendA", "A-Jugend (U19)"),
        ("JugendB", "B-Jugend (U17)"),
        ("JugendC", "C-Jugend (U15)"),
    };

    public static void Zeige(Control caller, Spieler spieler)
    {
        var menu = new PopupMenu();
        menu.AddItem("👤  Spielerdetails anzeigen", 0);

        menu.AddSeparator("Verschieben nach");

        // Nur Ziele anbieten, in denen der Spieler auch auflaufen darf - was zulässig ist,
        // entscheidet das Backend nach der Jugendordnung; hier wird dieselbe Regel gespiegelt,
        // damit gar nicht erst angeboten wird, was ohnehin abgelehnt würde.
        var ziele = new Dictionary<long, string>();
        long id = 10;
        foreach (var (schluessel, anzeige) in Kader)
        {
            if (schluessel == spieler.Kader) continue;
            if (!DarfWechseln(spieler, schluessel)) continue;

            menu.AddItem($"→  {anzeige}", (int)id);
            ziele[id] = schluessel;
            id++;
        }

        caller.AddChild(menu);
        menu.IdPressed += gewaehlt =>
        {
            if (gewaehlt == 0)
            {
                SpielerDetailOverlay.Zeige(caller, spieler);
            }
            else if (ziele.TryGetValue(gewaehlt, out var ziel))
            {
                _ = VerschiebeAsync(spieler, ziel);
            }
        };
        menu.PopupHide += menu.QueueFree;

        menu.Position = (Vector2I)caller.GetGlobalMousePosition();
        menu.Popup();
    }

    /// <summary>
    /// Spiegelt die Regeln aus <c>Kaderregeln</c> im Backend: In den Herrenbereich geht es ab
    /// 16 Jahren, eine Jugendklasse ist nach oben durch ihr Höchstalter begrenzt.
    /// </summary>
    private static bool DarfWechseln(Spieler spieler, string ziel)
    {
        int alter = spieler.Alter;
        return ziel switch
        {
            "Profi" or "Amateur" => alter >= 16,
            "JugendA"            => alter <= 18,
            "JugendB"            => alter <= 16,
            "JugendC"            => alter <= 14,
            _                    => false,
        };
    }

    private static async System.Threading.Tasks.Task VerschiebeAsync(Spieler spieler, string ziel)
    {
        var ergebnis = await ApiClient.PutAsync<object, Spieler>(
            $"spieler/{spieler.Id}/kader?ziel={ziel}", new { });

        if (ergebnis == null)
        {
            GD.PrintErr($"Kaderwechsel für {spieler.Name} nach {ziel} abgelehnt.");
            return;
        }

        spieler.Kader = ergebnis.Kader;
        KaderGeaendert?.Invoke();
    }
}
