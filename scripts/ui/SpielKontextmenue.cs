using Godot;

namespace FMDesktop.UI;

/// <summary>Rechtsklick-Kontextmenü für eine Zeile im Spielplan.</summary>
public static class SpielKontextmenue
{
    public static void Zeige(Control caller, long spielId, bool gespielt)
    {
        var menu = new PopupMenu();
        menu.AddItem("📋  Spielbericht anzeigen", 0);
        if (!gespielt)
        {
            // Noch nicht ausgetragen - Eintrag sichtbar lassen, aber deaktivieren.
            menu.SetItemDisabled(0, true);
            menu.AddSeparator();
            menu.AddItem("Noch nicht gespielt", 1);
            menu.SetItemDisabled(1, true);
        }

        caller.AddChild(menu);
        menu.IdPressed += id =>
        {
            if (id == 0) SpielberichtOverlay.Zeige(caller, spielId);
        };
        menu.PopupHide += menu.QueueFree;

        menu.Position = (Vector2I)caller.GetGlobalMousePosition();
        menu.Popup();
    }
}
