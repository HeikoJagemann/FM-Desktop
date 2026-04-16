#nullable enable
using Godot;
using System.Collections.Generic;
using System.Linq;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Liga;

public partial class StatistikenView : Control
{
    public override async void _Ready()
    {
        BuildUI();
        await LadeStatistiken();
    }

    private VBoxContainer _root = null!;

    private void BuildUI()
    {
        var scroll = new ScrollContainer();
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(scroll);

        _root = new VBoxContainer();
        _root.AddThemeConstantOverride("separation", 16);
        _root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_root);

        _root.AddChild(FmTheme.MakeLabel("📊  Statistiken", 20, FmTheme.TextPrimary));
        _root.AddChild(FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary));
    }

    private async System.Threading.Tasks.Task LadeStatistiken()
    {
        var ligaId  = GameState.Instance.LigaId;
        var ergebnisse = await ApiClient.GetAsync<List<Spiel>>($"liga/{ligaId}/ergebnisse");

        // Status-Label entfernen
        if (_root.GetChildCount() > 1)
            _root.GetChild(1).QueueFree();

        if (ergebnisse == null || ergebnisse.Count == 0)
        {
            _root.AddChild(FmTheme.MakeLabel("Noch keine Spiele ausgetragen.", 13, FmTheme.TextSecondary));
            return;
        }

        // ── Gesamt ───────────────────────────────────────────
        int gesamt  = ergebnisse.Count;
        int tore    = ergebnisse.Sum(s => (s.HeimTore ?? 0) + (s.GastTore ?? 0));
        _root.AddChild(BaueStatCard("📋  Gesamt", new[] {
            ("Gespielte Spiele", gesamt.ToString()),
            ("Erzielte Tore",    tore.ToString()),
            ("Ø Tore / Spiel",   $"{(double)tore / gesamt:F2}"),
        }));

        // ── Treffsicherste Vereine ────────────────────────────
        var torschuetzen = ergebnisse
            .SelectMany(s => new[] {
                (Verein: s.HeimVerein, Tore: s.HeimTore ?? 0),
                (Verein: s.GastVerein, Tore: s.GastTore ?? 0),
            })
            .GroupBy(x => x.Verein?.Id)
            .Select(g => (Name: g.First().Verein?.Name ?? "", Tore: g.Sum(x => x.Tore)))
            .OrderByDescending(x => x.Tore)
            .Take(5)
            .ToList();

        _root.AddChild(BaueStatCard("⚽  Treffsicherste Vereine",
            torschuetzen.Select((x, i) => ($"{i + 1}. {x.Name}", $"{x.Tore} Tore")).ToArray()));

        // ── Beste Abwehren ────────────────────────────────────
        var abwehren = ergebnisse
            .SelectMany(s => new[] {
                (Verein: s.HeimVerein, Gegentore: s.GastTore ?? 0),
                (Verein: s.GastVerein, Gegentore: s.HeimTore ?? 0),
            })
            .GroupBy(x => x.Verein?.Id)
            .Select(g => (Name: g.First().Verein?.Name ?? "", Gegentore: g.Sum(x => x.Gegentore)))
            .OrderBy(x => x.Gegentore)
            .Take(5)
            .ToList();

        _root.AddChild(BaueStatCard("🛡  Beste Abwehren",
            abwehren.Select((x, i) => ($"{i + 1}. {x.Name}", $"{x.Gegentore} Gegentore")).ToArray()));

        // ── Meiste Siege ──────────────────────────────────────
        var siege = ergebnisse
            .SelectMany(s =>
            {
                if (!s.Gespielt) return Enumerable.Empty<(Verein?, int)>();
                if (s.HeimTore > s.GastTore) return new[] { (s.HeimVerein, 1) };
                if (s.GastTore > s.HeimTore) return new[] { (s.GastVerein, 1) };
                return Enumerable.Empty<(Verein?, int)>();
            })
            .GroupBy(x => x.Item1?.Id)
            .Select(g => (Name: g.First().Item1?.Name ?? "", Siege: g.Count()))
            .OrderByDescending(x => x.Siege)
            .Take(5)
            .ToList();

        _root.AddChild(BaueStatCard("🥇  Meiste Siege",
            siege.Select((x, i) => ($"{i + 1}. {x.Name}", $"{x.Siege} Siege")).ToArray()));
    }

    private static Control BaueStatCard(string titel, (string Label, string Wert)[] zeilen)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle());

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 16);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        vbox.AddChild(FmTheme.MakeLabel(titel, 15, FmTheme.TextPrimary));

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        vbox.AddChild(sep);

        foreach (var (lbl, val) in zeilen)
        {
            var row = new HBoxContainer();
            var labelNode = FmTheme.MakeLabel(lbl, 13, FmTheme.TextSecondary);
            labelNode.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(labelNode);
            row.AddChild(FmTheme.MakeLabel(val, 13, FmTheme.TextPrimary));
            vbox.AddChild(row);
        }

        return panel;
    }
}
