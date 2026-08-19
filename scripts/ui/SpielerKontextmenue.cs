using Godot;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>Rechtsklick-Kontextmenü für Spielerzeilen (Kader, Jugend, ...).</summary>
public static class SpielerKontextmenue
{
    public static void Zeige(Control caller, Spieler spieler)
    {
        var menu = new PopupMenu();
        menu.AddItem("👤  Spielerdetails anzeigen", 0);

        caller.AddChild(menu);
        menu.IdPressed += id =>
        {
            if (id == 0) SpielerDetailOverlay.Zeige(caller, spieler);
        };
        menu.PopupHide += menu.QueueFree;

        menu.Position = (Vector2I)caller.GetGlobalMousePosition();
        menu.Popup();
    }
}
