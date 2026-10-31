#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Transfer;

/// <summary>Was man mit einem fremden Spieler tun kann - aus der Transferliste wie aus den Scoutberichten.</summary>
public static class TransferAktionen
{
    /// <summary>
    /// Kontextmenü für einen fremden Spieler: bieten oder einen Scout gezielt auf ihn ansetzen.
    /// </summary>
    /// <param name="fensterOffen">bieten geht nur im offenen Transferfenster</param>
    /// <param name="meldung">bekommt das Ergebnis für die Statuszeile</param>
    public static void Zeige(Control caller, TransferSpieler spieler, bool fensterOffen, Action<string> meldung)
    {
        var menu = new PopupMenu();
        menu.AddItem($"Angebot für {spieler.Name} abgeben …", 0);
        menu.SetItemDisabled(0, !fensterOffen);
        if (!fensterOffen) menu.SetItemTooltip(0, "Das Transferfenster ist geschlossen.");
        menu.AddItem("Gezielt beobachten lassen …", 1);
        menu.AddItem($"Scoutbericht ({spieler.Kenntnis} %)", 2);

        caller.AddChild(menu);
        menu.IdPressed += id =>
        {
            if (id == 0) BietenDialog.Zeige(caller, spieler, meldung);
            else if (id == 1) _ = BeobachtenLassen(caller, spieler.SpielerId, spieler.Name, meldung);
            else if (id == 2) ScoutberichtDialog.Zeige(caller, spieler, fensterOffen, meldung);
        };
        menu.PopupHide += menu.QueueFree;
        menu.Position = (Vector2I)caller.GetGlobalMousePosition();
        menu.Popup();
    }

    /// <summary>Lässt den Nutzer einen seiner Scouts wählen und setzt ihn auf den Spieler an.</summary>
    public static async System.Threading.Tasks.Task BeobachtenLassen(Control caller, long spielerId, string name,
                                                                     Action<string> meldung)
    {
        var scouting = await ApiClient.GetAsync<ScoutingModel>($"scouting/{GameState.Instance.VereinId}");
        if (!GodotObject.IsInstanceValid(caller)) return;
        var scouts = scouting?.Scouts ?? new List<ScoutModel>();
        if (scouts.Count == 0)
        {
            meldung("Kein Scout beschäftigt - unter Transfers › Scouting einstellen.");
            return;
        }

        var menu = new PopupMenu();
        menu.AddItem($"Wer soll {name} beobachten?", -1);
        menu.SetItemDisabled(0, true);
        menu.AddSeparator();
        for (int i = 0; i < scouts.Count; i++)
        {
            var s = scouts[i];
            string bisher = string.IsNullOrEmpty(s.Auftrag) ? "frei" : s.Auftrag;
            menu.AddItem($"{s.Name} (Qualität {s.Qualitaet}) - {bisher}", i);
        }

        caller.AddChild(menu);
        menu.IdPressed += async id =>
        {
            if (id < 0 || id >= scouts.Count) return;
            var scout = scouts[(int)id];
            var antwort = await ApiClient.SendeAsync("PUT", $"scouting/scout/{scout.Id}/auftrag",
                new { art = "SPIELER", ziel = spielerId });
            meldung(antwort.Ok
                ? $"{scout.Name} beobachtet jetzt {name} - zweimal je Woche."
                : antwort.Fehler ?? "Der Auftrag wurde abgelehnt.");
        };
        menu.PopupHide += menu.QueueFree;
        menu.Position = (Vector2I)caller.GetGlobalMousePosition();
        menu.Popup();
    }
}
