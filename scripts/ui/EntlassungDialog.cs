#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>
/// Abgestiegen aus der Oberliga, und der Vorstand hat sich getrennt: drei Oberligisten bieten dir
/// einen Neuanfang - wie zu Beginn des Spiels. Die Karte lässt sich erst schließen, wenn du einen
/// Verein gewählt hast.
/// </summary>
public partial class EntlassungDialog : Overlaykarte
{
    private Action? _gewaehlt;
    private VBoxContainer _liste = null!;

    public EntlassungDialog() : base("Entlassen", 620)
    {
        SchliesstBeiKlickDaneben = false;
    }

    public static void Zeige(Node caller, Action gewaehlt)
    {
        Oeffne(caller, new EntlassungDialog { _gewaehlt = gewaehlt }, "entlassung");
    }

    protected override void BaueInhalt(VBoxContainer inhalt)
    {
        SetzeUntertitel($"{GameState.Instance.VereinName} ist aus der Oberliga abgestiegen.");
        var text = FmTheme.MakeLabel("Der Vorstand trennt sich von dir. Drei Oberligisten suchen einen Trainer - "
                                     + "such dir deinen neuen Verein aus.", 14, FmTheme.TextPrimary);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        inhalt.AddChild(text);
        _liste = new VBoxContainer();
        _liste.AddThemeConstantOverride("separation", 8);
        inhalt.AddChild(_liste);
        _ = LadeAngebote();
    }

    private async Task LadeAngebote()
    {
        var vereine = await ApiClient.GetAsync<List<Verein>>("verein/oberliga/zufaellig");
        if (!IsInstanceValid(this) || vereine == null) return;
        foreach (var v in vereine)
        {
            var knopf = new Button
            {
                Text = $"{v.Name}\n{v.Liga?.Name}  ·  Stärke {v.Staerke}",
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 56),
            };
            FmTheme.ApplySecondaryButton(knopf);
            knopf.Pressed += async () => await Waehle(v);
            _liste.AddChild(knopf);
        }
    }

    private async Task Waehle(Verein verein)
    {
        await ApiClient.PostAsync<object, SpielstandInfo>("spielstand", new { vereinId = verein.Id });
        GameState.Instance.SetVerein(verein.Id, verein.Name, verein.Liga?.Id ?? 0, verein.Liga?.Name ?? "");
        Common.LigaAuswahl.Gewaehlt = null;
        _gewaehlt?.Invoke();
        Schliessen();
    }
}
