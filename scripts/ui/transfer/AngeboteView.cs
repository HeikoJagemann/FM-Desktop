#nullable enable
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Transfer;

/// <summary>
/// Angebote in beide Richtungen: was andere Vereine für eigene Spieler bieten, und wie die
/// abgefragten Vereine auf die eigenen Angebote geantwortet haben - samt Begründung einer Absage.
/// Vereinbarte Wechsel stehen in beiden Listen, bis sie mit dem Fensterstart vollzogen sind.
/// </summary>
public partial class AngeboteView : Control
{
    private Label _statusLabel = null!;
    private Label _fensterLabel = null!;
    private Label _eingehendLabel = null!;
    private Label _ausgehendLabel = null!;
    private FmGrid<TransferangebotModel> _eingehend = null!;
    private FmGrid<TransferangebotModel> _ausgehend = null!;
    private bool _fensterOffen;

    private bool Lebt => IsInstanceValid(this) && !IsQueuedForDeletion();

    public override async void _Ready()
    {
        BuildUI();
        await Lade();
    }

    private void BuildUI()
    {
        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 10);
        AddChild(vbox);

        vbox.AddChild(FmTheme.MakeTitel("Angebote"));
        _fensterLabel = FmTheme.MakeLabel("", 13, FmTheme.TextSecondary);
        vbox.AddChild(_fensterLabel);
        _statusLabel = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        vbox.AddChild(_statusLabel);

        _eingehendLabel = FmTheme.MakeLabel("Angebote für eigene Spieler", 15, FmTheme.TextPrimary);
        vbox.AddChild(_eingehendLabel);
        _eingehend = new FmGrid<TransferangebotModel>(TransferSpalten.Angebotsliste(true))
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _eingehend.Rechtsklick += ZeigeAngebotsmenue;
        vbox.AddChild(_eingehend);

        _ausgehendLabel = FmTheme.MakeLabel("Eigene Angebote", 15, FmTheme.TextPrimary);
        vbox.AddChild(_ausgehendLabel);
        _ausgehend = new FmGrid<TransferangebotModel>(TransferSpalten.Angebotsliste(false))
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        vbox.AddChild(_ausgehend);
    }

    private async Task Lade()
    {
        var fenster = await ApiClient.GetAsync<TransferfensterModel>("transfer/fenster");
        if (!Lebt) return;
        _fensterOffen = fenster?.Offen ?? false;
        _fensterLabel.Text = fenster?.Hinweis ?? "";
        _fensterLabel.AddThemeColorOverride("font_color", _fensterOffen ? FmTheme.Success : FmTheme.TextSecondary);

        long vereinId = GameState.Instance.VereinId;
        var eingehend = await ApiClient.GetAsync<List<TransferangebotModel>>($"transfer/eingehend/{vereinId}");
        var ausgehend = await ApiClient.GetAsync<List<TransferangebotModel>>($"transfer/ausgehend/{vereinId}");
        if (!Lebt) return;

        _eingehend.Zeige(eingehend ?? new List<TransferangebotModel>());
        _ausgehend.Zeige(ausgehend ?? new List<TransferangebotModel>());
        int offen = eingehend?.FindAll(a => !a.Vereinbart).Count ?? 0;
        _eingehendLabel.Text = eingehend is not { Count: > 0 }
            ? "Angebote für eigene Spieler - derzeit keine"
            : offen > 0
                ? $"Angebote für eigene Spieler ({offen}) - Rechtsklick zum Entscheiden"
                : "Angebote für eigene Spieler - nur vereinbarte Abgänge";
        _ausgehendLabel.Text = ausgehend is { Count: > 0 }
            ? $"Eigene Angebote ({ausgehend.Count}) - offene und die Antworten der letzten vier Wochen"
            : "Eigene Angebote - keine. Geboten wird aus der Transferliste oder den Scoutberichten.";
        // Nur beim ersten Laden - danach steht dort das Ergebnis der letzten Entscheidung.
        if (_statusLabel.Text == "Lade …")
            _statusLabel.Text = "Spieler auf die Transferliste setzt du im Kader per Rechtsklick.";
    }

    /// <summary>Eingehende Angebote annehmen oder ablehnen.</summary>
    private void ZeigeAngebotsmenue(TransferangebotModel angebot)
    {
        // Vereinbart ist entschieden - es wartet nur noch auf das Fenster.
        if (angebot.Vereinbart) return;

        var menu = new PopupMenu();
        menu.AddItem($"{angebot.SpielerName} → {angebot.NachVerein}", -1);
        menu.SetItemDisabled(0, true);
        menu.AddSeparator();
        menu.AddItem($"Annehmen ({FmTheme.Geld(angebot.Abloese)})", 0);
        menu.AddItem("Ablehnen", 1);
        if (!_fensterOffen)
            menu.SetItemTooltip(2, "Das Transferfenster ist geschlossen - er wechselt mit seiner Öffnung.");

        AddChild(menu);
        menu.IdPressed += async id =>
        {
            if (id == 0) await Entscheide(angebot, "annehmen");
            else if (id == 1) await Entscheide(angebot, "ablehnen");
        };
        menu.PopupHide += menu.QueueFree;
        menu.Position = (Vector2I)GetGlobalMousePosition();
        menu.Popup();
    }

    private async Task Entscheide(TransferangebotModel angebot, string aktion)
    {
        var antwort = await ApiClient.SendeAsync("POST", $"transfer/angebot/{angebot.Id}/{aktion}");
        if (!Lebt) return;

        _statusLabel.Text = !antwort.Ok
            ? antwort.Fehler ?? $"Das Angebot für {angebot.SpielerName} konnte nicht bearbeitet werden."
            : aktion != "annehmen"
                ? $"Angebot für {angebot.SpielerName} abgelehnt."
                : _fensterOffen
                    ? $"{angebot.SpielerName} wechselt für {FmTheme.Geld(angebot.Abloese)} zu {angebot.NachVerein}."
                    : $"Einigung: {angebot.SpielerName} wechselt für {FmTheme.Geld(angebot.Abloese)} zu "
                      + $"{angebot.NachVerein}, sobald das Transferfenster öffnet.";
        await Lade();
    }
}
