#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Common;

/// <summary>
/// Auswahl der Liga für Tabelle und Spielplan: die deutschen Ligen von oben nach unten, danach
/// die ersten Ligen des Auslands nach Land. Die Wahl bleibt für die Sitzung erhalten - wer sich
/// die Premier League ansieht, will dort meist auch den Spielplan sehen.
/// </summary>
public static class LigaAuswahl
{
    /// <summary>Die zuletzt gewählte Liga; leer heißt: die eigene.</summary>
    public static long? Gewaehlt { get; set; }

    public static long Aktuell => Gewaehlt ?? GameState.Instance.LigaId;

    /// <summary>Baut die Auswahl und meldet jede Änderung.</summary>
    public static async Task<OptionButton> Baue(Action<Models.Liga> geaendert)
    {
        var knopf = new OptionButton { CustomMinimumSize = new Vector2(320, 0), FitToLongestItem = false };
        var ligen = await ApiClient.GetAsync<List<Models.Liga>>("liga") ?? new List<Models.Liga>();
        var sichtbar = ligen.Where(l => !l.Ruhend).ToList();
        var deutsche = sichtbar.Where(l => l.Land == "GER").OrderBy(l => l.Stufe).ThenBy(l => l.Name).ToList();
        var ausland = sichtbar.Where(l => l.Land != "GER").OrderBy(l => l.LandName).ToList();

        var folge = new List<Models.Liga>();
        knopf.AddSeparator("Deutschland");
        foreach (var l in deutsche)
        {
            knopf.AddItem(l.Name);
            knopf.SetItemMetadata(knopf.ItemCount - 1, l.Id);
            folge.Add(l);
        }
        knopf.AddSeparator("Ausland");
        foreach (var l in ausland)
        {
            knopf.AddItem($"{l.LandName} – {l.Name}");
            knopf.SetItemMetadata(knopf.ItemCount - 1, l.Id);
            folge.Add(l);
        }
        for (int i = 0; i < knopf.ItemCount; i++)
        {
            if (!knopf.IsItemSeparator(i) && (long)knopf.GetItemMetadata(i) == Aktuell)
            {
                knopf.Select(i);
            }
        }
        knopf.ItemSelected += index =>
        {
            long id = (long)knopf.GetItemMetadata((int)index);
            var liga = folge.FirstOrDefault(l => l.Id == id);
            if (liga == null) return;
            Gewaehlt = liga.Id == GameState.Instance.LigaId ? null : liga.Id;
            geaendert(liga);
        };
        return knopf;
    }
}
