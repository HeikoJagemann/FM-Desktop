#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FMDesktop.Models;
using FMDesktop.UI.Taktik;

namespace FMDesktop.UI;

/// <summary>
/// Umstellen von der Seitenlinie: links die eigene Elf auf dem Rasen, rechts die Taktik.
///
/// <para>Die Spieler lassen sich ziehen wie in der Aufstellung vor der Partie - wo einer steht,
/// ergibt seine Position. Eine Vorlage setzt alle auf einmal um. Was hier geändert wird, gilt nur
/// für diese Partie; die gespeicherte Aufstellung und Taktik bleiben. Die Spieluhr steht, solange
/// der Dialog offen ist.</para>
/// </summary>
public partial class LiveTaktikDialog : Overlaykarte
{
    private const float FeldBreite = 380f;
    private const float FeldHoehe  = 460f;
    private const float PlaketteB  = PositionSlot.PlaketteBreite;
    private const float PlaketteH  = PositionSlot.PlaketteHoehe;

    private LiveTaktik _stand = null!;
    private List<FormationsVorlage> _vorlagen = new();
    private Action<LiveTaktik>? _beiBestaetigung;
    private Action? _beiAbbruch;
    private bool _bestaetigt;

    private TaktikModel _taktik = new();
    private readonly Dictionary<long, Vector2> _stellen = new();
    private readonly Dictionary<long, PositionSlot> _plaketten = new();
    private Control _rasen = null!;
    private Label _formationLabel = null!;
    private OptionButton _vorlagenAuswahl = null!;

    public LiveTaktikDialog() : base("Umstellen", 1040)
    {
        SchliesstBeiKlickDaneben = false;
    }

    public static void Zeige(Node caller, LiveTaktik stand, List<FormationsVorlage> vorlagen,
                             Action<LiveTaktik> beiBestaetigung, Action? beiAbbruch = null)
    {
        Oeffne(caller, new LiveTaktikDialog
        {
            _stand = stand,
            _vorlagen = vorlagen,
            _beiBestaetigung = beiBestaetigung,
            _beiAbbruch = beiAbbruch,
        });
    }

    protected override void BaueInhalt(VBoxContainer inhalt)
    {
        SetzeUntertitel("Gilt ab sofort und nur für diese Partie. Spieler ziehen ändert ihre Position.");

        var t = _stand.Taktik ?? new TaktikModel();
        _taktik = new TaktikModel
        {
            Ausrichtung = t.Ausrichtung, Deckung = t.Deckung, Gegenpressing = t.Gegenpressing,
            Passrichtung = t.Passrichtung, Passlaenge = t.Passlaenge,
        };
        foreach (var p in _stand.Plaetze)
            _stellen[p.SpielerId] = new Vector2((float)p.X, (float)p.Y);

        var spalten = new HBoxContainer();
        spalten.AddThemeConstantOverride("separation", 24);
        inhalt.AddChild(spalten);
        spalten.AddChild(BaueFormation());
        spalten.AddChild(BaueTaktik());

        inhalt.AddChild(Knopfleiste("Übernehmen", Bestaetige));
    }

    protected override void BeimSchliessen()
    {
        if (!_bestaetigt) _beiAbbruch?.Invoke();
    }

    // ── Formation ────────────────────────────────────────────────────────────

    private Control BaueFormation()
    {
        var spalte = new VBoxContainer();
        spalte.AddThemeConstantOverride("separation", 8);

        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 12);
        _formationLabel = FmTheme.MakeAbschnitt(_stand.Formation, 20);
        _formationLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        kopf.AddChild(_formationLabel);
        _vorlagenAuswahl = new OptionButton { CustomMinimumSize = new Vector2(170, 0) };
        _vorlagenAuswahl.TooltipText = "Stellt alle Feldspieler nach einer Vorlage auf - jeder geht auf den nächsten Platz.";
        _vorlagenAuswahl.AddItem("Vorlage wählen …");
        _vorlagenAuswahl.SetItemDisabled(0, true);
        foreach (var v in _vorlagen) _vorlagenAuswahl.AddItem(v.Name);
        _vorlagenAuswahl.Select(0);
        _vorlagenAuswahl.ItemSelected += i => { if (i > 0) WendeVorlageAn(_vorlagen[(int)i - 1]); };
        kopf.AddChild(_vorlagenAuswahl);
        spalte.AddChild(kopf);

        _rasen = new Rasen { CustomMinimumSize = new Vector2(FeldBreite, FeldHoehe) };
        spalte.AddChild(_rasen);

        var namen = _stand.Plaetze.Select(p => p.Slot).ToList();
        foreach (var p in _stand.Plaetze)
        {
            var plakette = PositionSlot.Create(p.Slot, p.Position);
            plakette.Assign(p.SpielerId, p.Name, StaerkeAuf(p, p.Position));
            plakette.Size = new Vector2(PlaketteB, PlaketteH);
            plakette.Verschiebbar = !p.Torwart;
            long id = p.SpielerId;
            var platz = p;
            plakette.Zieht += (s, mitte) =>
            {
                var stelle = VomRasen(mitte);
                ZeigePosition(platz, s, Platzierung.PositionAus(stelle.X, stelle.Y));
            };
            plakette.Abgelegt += s =>
            {
                _stellen[id] = VomRasen(s.Position + s.Size / 2);
                ZeigeFormation();
            };
            _plaketten[id] = plakette;
            _rasen.AddChild(plakette);
            SetzePlakette(id);
        }
        return spalte;
    }

    /// <summary>Wie in der Aufstellung: das eigene Tor unten, leicht gestaucht.</summary>
    private static Vector2 AufRasen(Vector2 stelle) => new(
        FeldBreite * (0.5f + (stelle.Y - 0.5f) * 0.95f),
        FeldHoehe * (0.93f - 0.9f * stelle.X));

    private static Vector2 VomRasen(Vector2 mitte) => new(
        Mathf.Clamp((0.93f - mitte.Y / FeldHoehe) / 0.9f, (float)Platzierung.XMin, (float)Platzierung.XMax),
        Mathf.Clamp(0.5f + (mitte.X / FeldBreite - 0.5f) / 0.95f, (float)Platzierung.YMin, (float)Platzierung.YMax));

    private void SetzePlakette(long id)
    {
        var plakette = _plaketten[id];
        var mitte = AufRasen(_stellen[id]);
        plakette.Position = new Vector2(
            Mathf.Clamp(mitte.X - PlaketteB / 2, 0, FeldBreite - PlaketteB),
            Mathf.Clamp(mitte.Y - PlaketteH / 2, 0, FeldHoehe - PlaketteH));
        if (plakette.Verschiebbar)
            ZeigePosition(_stand.Plaetze.First(p => p.SpielerId == id), plakette,
                Platzierung.PositionAus(_stellen[id].X, _stellen[id].Y));
    }

    /// <summary>Position und die Stärke, die der Spieler dort hätte.</summary>
    private static void ZeigePosition(LivePlatz platz, PositionSlot plakette, string position)
    {
        var staerke = StaerkeAuf(platz, position);
        if (staerke.HasValue) plakette.UpdateStaerke(staerke.Value);
        plakette.ZeigeVorschau(position);
    }

    private static int? StaerkeAuf(LivePlatz platz, string position) =>
        platz.Staerken != null && platz.Staerken.TryGetValue(position, out var s) ? s : null;

    /// <summary>
    /// Zeigt, wie viele Spieler in welcher Reihe stehen - grob wie der Server, der die genaue
    /// Formation nach dem Übernehmen benennt.
    /// </summary>
    private void ZeigeFormation()
    {
        var tiefen = _stand.Plaetze.Where(p => !p.Torwart).Select(p => _stellen[p.SpielerId].X).OrderBy(x => x).ToList();
        if (tiefen.Count == 0) return;
        var reihen = new List<int>();
        int inReihe = 1;
        for (int i = 1; i < tiefen.Count; i++)
        {
            if (tiefen[i] - tiefen[i - 1] >= 0.08f) { reihen.Add(inReihe); inReihe = 0; }
            inReihe++;
        }
        reihen.Add(inReihe);
        _formationLabel.Text = string.Join("-", reihen);
    }

    /// <summary>
    /// Stellt die Feldspieler nach einer Vorlage auf: immer das Paar aus Spieler und freiem Platz
    /// mit dem kürzesten Weg zuerst - so wandert jeder möglichst wenig. Nach einem Platzverweis
    /// bleibt ein Vorlagenplatz frei.
    /// </summary>
    private void WendeVorlageAn(FormationsVorlage vorlage)
    {
        var spieler = _stand.Plaetze.Where(p => !p.Torwart).Select(p => p.SpielerId).ToList();
        var plaetze = vorlage.Plaetze.Where(p => SlotBezeichnung.Basis(p.Slot) != "TW")
            .Select(p => new Vector2((float)p.X, (float)p.Y)).ToList();
        var paare = new List<(float Weg, long Id, int Platz)>();
        foreach (var id in spieler)
            for (int i = 0; i < plaetze.Count; i++)
                paare.Add((_stellen[id].DistanceTo(plaetze[i]), id, i));

        var vergeben = new HashSet<long>();
        var belegt = new HashSet<int>();
        foreach (var (_, id, platz) in paare.OrderBy(p => p.Weg))
        {
            if (vergeben.Contains(id) || belegt.Contains(platz)) continue;
            vergeben.Add(id);
            belegt.Add(platz);
            _stellen[id] = plaetze[platz];
            SetzePlakette(id);
        }
        _formationLabel.Text = vorlage.Name;
    }

    // ── Taktik ───────────────────────────────────────────────────────────────

    private Control BaueTaktik()
    {
        var spalte = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        spalte.AddThemeConstantOverride("separation", 14);
        foreach (var e in TaktikView.Einstellungen)
            spalte.AddChild(Einstellung(e));
        return spalte;
    }

    private Control Einstellung(TaktikView.Einstellung e)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 5);

        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 10);
        kopf.AddChild(FmTheme.MakeAbschnitt(e.Titel, 16));
        if (!e.WirktNoch)
        {
            var hinweis = FmTheme.MakeLabel("WIRKT NOCH NICHT", 10, FmTheme.Gold);
            hinweis.AddThemeFontOverride("font", FmTheme.SchriftUi(700));
            hinweis.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            kopf.AddChild(hinweis);
        }
        box.AddChild(kopf);

        int auswahl = Math.Max(0, Array.IndexOf(e.Werte, e.Lesen(_taktik)));
        var schalter = Segmentschalter.Neu(e.Texte, auswahl, new Vector2(e.Knopfbreite * 0.72f, 30), 12);
        box.AddChild(schalter);

        var wirkung = FmTheme.MakeLabel(e.Wirkung[auswahl], 12, FmTheme.TextSecondary);
        wirkung.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        wirkung.CustomMinimumSize = new Vector2(520, 0);
        box.AddChild(wirkung);

        schalter.Gewaehlt += i =>
        {
            e.Setzen(_taktik, e.Werte[i]);
            wirkung.Text = e.Wirkung[i];
        };
        return box;
    }

    // ── Übernehmen ───────────────────────────────────────────────────────────

    private void Bestaetige()
    {
        _bestaetigt = true;
        var neu = new LiveTaktik
        {
            Taktik = _taktik,
            Plaetze = _stand.Plaetze.Select(p => new LivePlatz
            {
                SpielerId = p.SpielerId,
                X = _stellen[p.SpielerId].X,
                Y = _stellen[p.SpielerId].Y,
            }).ToList(),
        };
        _beiBestaetigung?.Invoke(neu);
        Schliessen();
    }

    /// <summary>Für den Vorschaumodus: wählt eine Vorlage, als hätte der Nutzer sie ausgesucht.</summary>
    internal void VorschauVorlage(string name)
    {
        int index = _vorlagen.FindIndex(v => v.Name == name);
        if (index < 0) return;
        _vorlagenAuswahl.Select(index + 1);
        WendeVorlageAn(_vorlagen[index]);
    }
}
