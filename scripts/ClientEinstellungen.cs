#nullable enable
using Godot;

namespace FMDesktop;

/// <summary>
/// Lokale UI-Einstellungen des Clients - Dinge wie ein-/ausgeblendete Filter. Bleiben über einen
/// Neustart der Anwendung erhalten, gehören aber nicht zum Spielstand: Sie liegen auf diesem
/// Rechner, nicht im Backend, und wechseln nicht mit dem Verein oder Spielstand.
/// </summary>
public static class ClientEinstellungen
{
    private const string Pfad = "user://einstellungen.cfg";
    private const string Abschnitt = "ui";

    private static ConfigFile? _cfg;

    private static ConfigFile Laden()
    {
        if (_cfg != null) return _cfg;
        _cfg = new ConfigFile();
        // Fehlt die Datei beim ersten Start, bleibt _cfg leer - GetValue liefert dann die
        // übergebenen Standardwerte.
        _cfg.Load(Pfad);
        return _cfg;
    }

    public static bool GetBool(string schluessel, bool standard) =>
        Laden().GetValue(Abschnitt, schluessel, standard).AsBool();

    public static void SetBool(string schluessel, bool wert)
    {
        var cfg = Laden();
        cfg.SetValue(Abschnitt, schluessel, wert);
        cfg.Save(Pfad);
    }
}
