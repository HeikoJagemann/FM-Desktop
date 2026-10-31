#nullable enable
using Godot;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Transfer;

/// <summary>
/// Die Zusammenfassung einer Scoutingwoche - geöffnet aus der Wochenmeldung im Dashboard.
///
/// <para>Oben die Zahlen und wer wie viel gesehen hat, darunter die auffälligsten Spieler und die
/// Liste aller Beobachteten. Die Liste verhält sich wie die Scoutberichte: Doppelklick öffnet den
/// Bericht, Rechtsklick das Angebot. Die Spannen sind der Stand von heute.</para>
/// </summary>
public partial class ScoutwocheOverlay : Overlaykarte
{
    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private DateOnly _sonntag;
    private Action? _zumScouting;
    private VBoxContainer _inhalt = null!;
    private Label _status = null!;

    public ScoutwocheOverlay() : base("Scoutberichte der Woche", 1340)
    {
    }

    /// <param name="zumScouting">öffnet die Scoutingseite - die Karte schließt sich davor</param>
    public static void Zeige(Node caller, DateOnly sonntag, Action? zumScouting = null)
    {
        Oeffne(caller, new ScoutwocheOverlay { _sonntag = sonntag, _zumScouting = zumScouting },
            sonntag.ToString("yyyy-MM-dd"));
    }

    protected override void BaueInhalt(VBoxContainer inhalt)
    {
        SetzeUntertitel($"Woche bis {_sonntag.ToString("dddd, dd.MM.yyyy", Deutsch)}");
        _inhalt = new VBoxContainer();
        _inhalt.AddThemeConstantOverride("separation", 12);
        inhalt.AddChild(_inhalt);
        _status = FmTheme.MakeLabel("Lade …", 13, FmTheme.TextSecondary);
        _inhalt.AddChild(_status);

        var leiste = new HBoxContainer();
        leiste.AddThemeConstantOverride("separation", 10);
        var hinweis = FmTheme.MakeLabel("Doppelklick: Scoutbericht  ·  Rechtsklick: Angebot und mehr", 12, FmTheme.TextGedimmt);
        hinweis.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        leiste.AddChild(hinweis);
        if (_zumScouting != null)
        {
            var scouting = new Button { Text = "Zum Scouting" };
            FmTheme.ApplySecondaryButton(scouting);
            scouting.Pressed += () => { var ziel = _zumScouting; Schliessen(); ziel?.Invoke(); };
            leiste.AddChild(scouting);
        }
        var schliessen = new Button { Text = "Schließen" };
        FmTheme.ApplyPrimaryButton(schliessen);
        schliessen.Pressed += Schliessen;
        leiste.AddChild(schliessen);
        inhalt.AddChild(leiste);

        _ = Lade();
    }

    private async Task Lade()
    {
        var woche = await ApiClient.GetAsync<Scoutwoche>(
            $"scouting/woche?vereinId={GameState.Instance.VereinId}&sonntag={_sonntag:yyyy-MM-dd}");
        if (!IsInstanceValid(this)) return;
        if (woche == null)
        {
            _status.Text = "Die Zusammenfassung konnte nicht geladen werden.";
            return;
        }
        if (woche.Beobachtungen == 0)
        {
            _status.Text = "Für diese Woche liegt keine Aufzeichnung vor - festgehalten werden die Beobachtungen der letzten zwölf Wochen.";
            return;
        }
        _status.QueueFree();
        Zeige(woche);
    }

    private void Zeige(Scoutwoche w)
    {
        // ── Zahlen ──
        var tafeln = new HBoxContainer();
        tafeln.AddThemeConstantOverride("separation", 10);
        tafeln.AddChild(Tafel("Beobachtungen", $"{w.Beobachtungen}", "Ein Spieler kann mehrmals beobachtet worden sein."));
        tafeln.AddChild(Tafel("Spieler", $"{w.AnzahlSpieler}", "Verschiedene Spieler, über die jetzt ein Bericht vorliegt."));
        tafeln.AddChild(Tafel("Zum ersten Mal", $"{w.Erstmals}", "Über diese Spieler gab es vorher keinen Bericht."));
        tafeln.AddChild(Tafel("Auf der Transferliste", $"{w.AufTransferliste}", "Diese Spieler sind zu haben - zum Listenpreis."));
        _inhalt.AddChild(tafeln);

        // ── Wer was gesehen hat, und die Auffälligsten ──
        var mitte = new HBoxContainer();
        mitte.AddThemeConstantOverride("separation", 24);
        _inhalt.AddChild(mitte);

        var quellen = new VBoxContainer { CustomMinimumSize = new Vector2(330, 0) };
        quellen.AddThemeConstantOverride("separation", 4);
        quellen.AddChild(FmTheme.MakeAbschnitt("Beobachtet von", 16));
        foreach (var q in w.Quellen)
        {
            var zeile = new HBoxContainer();
            var name = FmTheme.MakeLabel(q.Name == "Spielbeobachtung" ? "Eigene Partien (Gegner)" : q.Name, 13, FmTheme.TextPrimary);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            zeile.AddChild(name);
            zeile.AddChild(FmTheme.MakeLabel(q.Beobachtungen == q.Spieler
                ? $"{q.Spieler} Spieler"
                : $"{q.Spieler} Spieler, {q.Beobachtungen} Beobachtungen", 13, FmTheme.TextSecondary));
            quellen.AddChild(zeile);
        }
        mitte.AddChild(quellen);

        var auffaellig = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        auffaellig.AddThemeConstantOverride("separation", 4);
        auffaellig.AddChild(FmTheme.MakeAbschnitt("Aufgefallen", 16));
        var nachId = w.Spieler.ToDictionary(s => s.SpielerId);
        foreach (var h in w.Hoehepunkte)
        {
            if (!nachId.TryGetValue(h.SpielerId, out var s)) continue;
            var zeile = new HBoxContainer();
            zeile.AddThemeConstantOverride("separation", 10);
            var titel = FmTheme.MakeLabel(h.Titel, 13, FmTheme.Accent);
            titel.CustomMinimumSize = new Vector2(150, 0);
            zeile.AddChild(titel);
            var name = FmTheme.MakeLabel($"{s.Name} ({s.Position}, {s.Alter}) · {s.Verein}", 13, FmTheme.TextPrimary);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.ClipText = true;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            zeile.AddChild(name);
            zeile.AddChild(FmTheme.MakeLabel($"Stärke {s.StaerkeText}", 13, FmTheme.TextSecondary));
            zeile.AddChild(FmTheme.MakeLabel(s.TalentText, 13, FmTheme.Gold));
            auffaellig.AddChild(zeile);
        }
        if (w.Hoehepunkte.Count == 0)
            auffaellig.AddChild(FmTheme.MakeLabel("Nichts Besonderes dabei.", 13, FmTheme.TextSecondary));
        mitte.AddChild(auffaellig);

        // ── Alle Beobachteten ──
        var neu = w.Neu.ToHashSet();
        var spalten = TransferSpalten.Berichte.ToList();
        spalten.Insert(1, new GridSpalte<TransferSpieler>
        {
            Titel = "", Breite = 44,
            Text    = s => neu.Contains(s.SpielerId) ? "NEU" : "",
            Farbe   = _ => FmTheme.Accent,
            Tooltip = s => neu.Contains(s.SpielerId) ? "Diese Woche zum ersten Mal beobachtet." : "",
        });
        var grid = new FmGrid<TransferSpieler>(spalten) { CustomMinimumSize = new Vector2(0, 360) };
        grid.Doppelklick += s => ScoutberichtDialog.Zeige(this, s, _ => { });
        grid.Rechtsklick += s => TransferAktionen.Zeige(this, s, _ => { });
        _inhalt.AddChild(grid);
        grid.Zeige(w.Spieler);
    }

    private static Control Tafel(string titel, string wert, string tooltip)
    {
        var tafel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = tooltip };
        var stil = FmTheme.Flaeche(FmTheme.BgDark, 10, FmTheme.Border);
        stil.ContentMarginLeft = stil.ContentMarginRight = 14;
        stil.ContentMarginTop = stil.ContentMarginBottom = 8;
        tafel.AddThemeStyleboxOverride("panel", stil);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 0);
        box.AddChild(FmTheme.MakeLabel(titel.ToUpperInvariant(), 10, FmTheme.TextSecondary));
        box.AddChild(FmTheme.MakeZahl(wert, 26, FmTheme.TextPrimary));
        tafel.AddChild(box);
        return tafel;
    }
}
