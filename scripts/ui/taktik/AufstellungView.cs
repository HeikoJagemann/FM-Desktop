#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FMDesktop.Api;
using FMDesktop.Models;
using FMDesktop.UI.Common;

namespace FMDesktop.UI.Taktik;

// ---------------------------------------------------------------------------
// Drop-target slot on the football field
// ---------------------------------------------------------------------------

/// <summary>
/// Ein Platz auf dem Feld oder auf der Bank - gezeichnet als Spielerplakette.
///
/// <para>Auf dem Feld: ein Ring in der Farbe des Mannschaftsteils, darin die Stärke auf dieser
/// Position, darunter Namensschild und Positionskürzel. Spielt jemand auf einer Position, die er
/// nicht gelernt hat, steht darunter, wie gut er dort eingespielt ist. Auf der Bank wird derselbe
/// Platz als kompakte Karte gezeichnet. Vorher war beides ein grüner Knopf mit drei Zeilen
/// Text.</para>
/// </summary>
public partial class PositionSlot : Button
{
    /// <summary>Technischer Schlüssel der Aufstellung, etwa "ZM_1".</summary>
    public string SlotName { get; private set; } = "";
    /// <summary>Was auf dem Platz steht, etwa "LZM" - der Schlüssel bleibt unverändert.</summary>
    public string Anzeige { get; private set; } = "";
    public long?  SpielerId { get; private set; }
    public string SpielerName { get; private set; } = "";
    public int?   Staerke { get; private set; }
    public int?   Grundstaerke { get; private set; }
    public int?   Eingespieltheit { get; private set; }
    /// <summary>Gelernte Position des Spielers - für die Bank, wo der Platz keine hat.</summary>
    public string? SpielerPosition { get; private set; }

    /// <summary>Bankplatz statt Feldposition - als Karte statt als Plakette gezeichnet.</summary>
    public bool IstBank { get; private set; }

    public event Action<PositionSlot, long, string>? PlayerDropped;

    /// <summary>Während die Plakette über den Rasen gezogen wird - ihre Mitte im Rasen.</summary>
    public event Action<PositionSlot, Vector2>? Zieht;

    /// <summary>Die Plakette wurde an einer neuen Stelle losgelassen.</summary>
    public event Action<PositionSlot>? Abgelegt;

    /// <summary>Feldspieler lassen sich verschieben; der Torwart bleibt in seinem Tor.</summary>
    public bool Verschiebbar { get; set; }

    /// <summary>Position während des Ziehens - so zeigt die Plakette schon, was sie gleich wird.</summary>
    private string? _vorschau;

    private bool _gedrueckt;
    private bool _zieht;
    private Vector2 _griff;
    private Vector2 _druckpunkt;

    /// <summary>Ab so vielen Pixeln wird aus einem Klick ein Verschieben.</summary>
    private const float Ziehschwelle = 4f;

    public const float PlaketteBreite = 96f;
    public const float PlaketteHoehe  = 80f;

    public static PositionSlot Create(string slotName, string anzeige, bool bank = false)
    {
        var slot = new PositionSlot { SlotName = slotName, Anzeige = anzeige, IstBank = bank };
        slot.CustomMinimumSize = bank ? new Vector2(0, 52) : new Vector2(PlaketteBreite, PlaketteHoehe);
        slot.FocusMode = FocusModeEnum.None;
        // Links wird verschoben, rechts geleert - ein Linksklick allein darf nichts entfernen.
        slot.ButtonMask = MouseButtonMask.Right;
        foreach (var zustand in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            slot.AddThemeStyleboxOverride(zustand, new StyleBoxEmpty());
        slot.MouseEntered += slot.QueueRedraw;
        slot.MouseExited  += slot.QueueRedraw;
        slot.Refresh();
        return slot;
    }

    public void Assign(long spielerId, string spielerName, int? staerke = null,
        int? grundstaerke = null, int? eingespieltheit = null, string? spielerPosition = null)
    {
        SpielerId       = spielerId;
        SpielerName     = spielerName;
        Staerke         = staerke;
        Grundstaerke    = grundstaerke;
        Eingespieltheit = eingespieltheit;
        SpielerPosition = spielerPosition;
        Refresh();
    }

    public void UpdateStaerke(int staerke, int? grundstaerke = null, int? eingespieltheit = null)
    {
        Staerke         = staerke;
        Grundstaerke    = grundstaerke;
        Eingespieltheit = eingespieltheit;
        Refresh();
    }

    public void Clear()
    {
        SpielerId       = null;
        SpielerName     = "";
        Staerke         = null;
        Grundstaerke    = null;
        Eingespieltheit = null;
        SpielerPosition = null;
        Refresh();
    }

    /// <summary>Mannschaftsteil: auf dem Feld der des Platzes, auf der Bank der des Spielers.</summary>
    private Positionsgruppe Gruppe => PositionsgruppeHelfer.Von(
        IstBank ? SpielerPosition : _vorschau ?? SlotBezeichnung.Basis(SlotName));

    /// <summary>Zeigt beim Ziehen die Position, die der Platz an dieser Stelle hätte.</summary>
    public void ZeigeVorschau(string position)
    {
        if (_vorschau == position) return;
        _vorschau = position;
        Anzeige = position;
        Refresh();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (!Verschiebbar || GetParent() is not Control rasen) return;

        // Ereigniskoordinaten sind lokal zur Plakette; auf den Rasen umgerechnet über ihre Lage.
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } knopf)
        {
            if (knopf.Pressed)
            {
                _gedrueckt = true;
                _druckpunkt = Position + knopf.Position;
                _griff = knopf.Position;
            }
            else if (_gedrueckt)
            {
                _gedrueckt = false;
                if (_zieht)
                {
                    _zieht = false;
                    Abgelegt?.Invoke(this);
                }
            }
            AcceptEvent();
        }
        else if (@event is InputEventMouseMotion bewegung && _gedrueckt)
        {
            var maus = Position + bewegung.Position;
            if (!_zieht && maus.DistanceTo(_druckpunkt) < Ziehschwelle) return;
            _zieht = true;
            var groesse = Size;
            var neu = maus - _griff;
            neu.X = Mathf.Clamp(neu.X, 0, rasen.Size.X - groesse.X);
            neu.Y = Mathf.Clamp(neu.Y, 0, rasen.Size.Y - groesse.Y);
            Position = neu;
            Zieht?.Invoke(this, neu + groesse / 2);
            AcceptEvent();
        }
    }

    private void Refresh()
    {
        Text = "";
        TooltipText = SpielerId.HasValue && Staerke.HasValue && Grundstaerke.HasValue && Eingespieltheit.HasValue
            ? StaerkeErklaerung.Basis(Anzeige, Grundstaerke.Value, Eingespieltheit.Value, Staerke.Value)
            : SpielerId.HasValue
                ? IstBank ? $"{SpielerName} - Rechtsklick entfernt" : $"{SpielerName} - ziehen verschiebt, Rechtsklick entfernt"
                : IstBank ? "Freier Bankplatz - Spieler hierher ziehen" : $"{Anzeige} - Spieler hierher ziehen, Platz verschieben mit der Maus";
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (IstBank) ZeichneKarte();
        else ZeichnePlakette();
    }

    private void ZeichnePlakette()
    {
        bool besetzt = SpielerId.HasValue;
        bool schwebend = IsHovered();
        var gruppenfarbe = FmTheme.GruppeFarbe(Gruppe);
        float mitteX = Size.X / 2f;
        var kreis = new Vector2(mitteX, 23);

        // Ring mit Stärke
        if (besetzt)
        {
            DrawCircle(kreis + new Vector2(0, 2), 23, new Color(0, 0, 0, 0.35f));
            DrawCircle(kreis, 22, new Color("0F1814"));
            DrawArc(kreis, 20.5f, 0, Mathf.Pi * 2, 48, schwebend ? gruppenfarbe.Lightened(0.25f) : gruppenfarbe, 3, true);
            string zahl = Staerke?.ToString() ?? "–";
            Zentriert(FmTheme.SchriftZahl, zahl, kreis.Y, 20, FmTheme.TextPrimary);
        }
        else
        {
            DrawCircle(kreis, 22, new Color(0.06f, 0.09f, 0.08f, schwebend ? 0.55f : 0.35f));
            DrawArc(kreis, 20.5f, 0, Mathf.Pi * 2, 48, new Color(1, 1, 1, schwebend ? 0.8f : 0.45f), 2, true);
            Zentriert(FmTheme.SchriftUi(700), "+", kreis.Y, 18, new Color(1, 1, 1, 0.7f));
        }

        // Fremde Position: wie gut er dort eingespielt ist - als Abzeichen am Ring, damit die
        // Plakette nicht höher wird als der Abstand zur nächsten Reihe.
        if (besetzt && Eingespieltheit is < 100)
        {
            string text = $"{Eingespieltheit} %";
            var schrift = FmTheme.SchriftUi(700);
            float breite = schrift.GetStringSize(text, HorizontalAlignment.Left, -1, 10).X + 10;
            var abzeichen = new Rect2(kreis.X + 12, kreis.Y - 25, breite, 16);
            DrawStyleBox(FmTheme.Flaeche(FmTheme.Accent, 8), abzeichen);
            DrawString(schrift, new Vector2(abzeichen.Position.X + 5, abzeichen.Position.Y + 12), text,
                HorizontalAlignment.Left, -1, 10, FmTheme.AccentText);
        }

        // Namensschild mit Positionskürzel: "Lenz · OM"
        float y = 50;
        var namenSchrift = FmTheme.SchriftUi(600);
        var kuerzelSchrift = FmTheme.SchriftUi(700);
        string kuerzel = Anzeige;
        float kuerzelBreite = kuerzelSchrift.GetStringSize(kuerzel, HorizontalAlignment.Left, -1, 10).X;
        var kuerzelFarbe = besetzt ? FmTheme.TextFuerGruppe(Gruppe) : new Color(1, 1, 1, 0.8f);
        if (besetzt)
        {
            string name = Nachname(SpielerName);
            float namenBreite = namenSchrift.GetStringSize(name, HorizontalAlignment.Left, -1, 12).X;
            float breite = Mathf.Min(Size.X, namenBreite + kuerzelBreite + 22);
            var schild = new Rect2(mitteX - breite / 2f, y, breite, 21);
            DrawStyleBox(FmTheme.Flaeche(new Color(0.035f, 0.055f, 0.047f, 0.85f), 6), schild);
            DrawString(namenSchrift, new Vector2(schild.Position.X + 8, y + 15), name,
                HorizontalAlignment.Left, breite - kuerzelBreite - 22, 12, FmTheme.TextPrimary);
            DrawString(kuerzelSchrift, new Vector2(schild.End.X - 8 - kuerzelBreite, y + 15), kuerzel,
                HorizontalAlignment.Left, -1, 10, kuerzelFarbe);
        }
        else
        {
            Zentriert(kuerzelSchrift, kuerzel, y + 10, 11, kuerzelFarbe);
        }
    }

    private void ZeichneKarte()
    {
        bool besetzt = SpielerId.HasValue;
        bool schwebend = IsHovered();
        var rahmen = new Rect2(Vector2.Zero, Size);
        var flaeche = FmTheme.Flaeche(FmTheme.BgPanel, 10,
            schwebend ? FmTheme.TextGedimmt : FmTheme.Border);
        if (!besetzt)
        {
            flaeche.BgColor = new Color(0, 0, 0, 0);
            flaeche.BorderColor = schwebend ? FmTheme.TextGedimmt : FmTheme.BorderStark;
        }
        DrawStyleBox(flaeche, rahmen);

        if (!besetzt)
        {
            Zentriert(FmTheme.SchriftUi(500), "frei", Size.Y / 2f, 12, FmTheme.TextGedimmt);
            return;
        }

        // Zeile 1: Positionschip und Stärke
        string position = SpielerPosition ?? "";
        var chipSchrift = FmTheme.SchriftUi(700);
        float chipBreite = Mathf.Max(30, chipSchrift.GetStringSize(position, HorizontalAlignment.Left, -1, 11).X + 12);
        var chip = new Rect2(10, 8, chipBreite, 18);
        DrawStyleBox(FmTheme.Flaeche(FmTheme.GruppeChip(Gruppe), 5), chip);
        DrawString(chipSchrift, new Vector2(chip.Position.X, chip.Position.Y + 13), position,
            HorizontalAlignment.Center, chipBreite, 11, FmTheme.TextFuerGruppe(Gruppe));

        if (Staerke.HasValue)
        {
            DrawString(FmTheme.SchriftZahl, new Vector2(10, 24), Staerke.Value.ToString(),
                HorizontalAlignment.Right, Size.X - 20, 20, FmTheme.TextPrimary);
        }

        // Zeile 2: Name
        DrawString(FmTheme.SchriftUi(600), new Vector2(10, 44), Nachname(SpielerName),
            HorizontalAlignment.Left, Size.X - 20, 13, FmTheme.TextPrimary);
    }

    /// <summary>Zeichnet Text waagerecht zentriert, mit der Mitte der Großbuchstaben auf <paramref name="mitteY"/>.</summary>
    private void Zentriert(Font schrift, string text, float mitteY, int groesse, Color farbe)
    {
        float hoehe = schrift.GetAscent(groesse) - schrift.GetDescent(groesse);
        DrawString(schrift, new Vector2(0, mitteY + hoehe / 2f - schrift.GetDescent(groesse)), text,
            HorizontalAlignment.Center, Size.X, groesse, farbe);
    }

    private static string Nachname(string name)
    {
        var parts = name.Split(' ');
        var nachname = parts[^1];
        return nachname.Length > 11 ? nachname[..10] + "." : nachname;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return data.VariantType == Variant.Type.Dictionary;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var dict      = data.AsGodotDictionary();
        var spielerId = dict["id"].AsInt64();
        var name      = dict["name"].AsString();
        PlayerDropped?.Invoke(this, spielerId, name);
    }
}

/// <summary>Der Rasen: Streifen, Seitenlinien, Mittelkreis und beide Strafräume.</summary>
public partial class Rasen : Control
{
    public override void _Draw()
    {
        var feld = new Rect2(Vector2.Zero, Size);
        DrawStyleBox(FmTheme.Flaeche(FmTheme.Rasen, 14, FmTheme.Border), feld);

        // Streifen - ohne die gerundeten Ecken zu überdecken
        const int Streifen = 10;
        float hoehe = Size.Y / Streifen;
        for (int i = 1; i < Streifen - 1; i += 2)
            DrawRect(new Rect2(1, i * hoehe, Size.X - 2, hoehe), FmTheme.RasenStreif);

        var linie = FmTheme.Linie;
        const float Rand = 14f;
        var spielfeld = new Rect2(Rand, Rand, Size.X - 2 * Rand, Size.Y - 2 * Rand);
        DrawRect(spielfeld, linie, false, 2);
        float mitteY = Size.Y / 2f;
        DrawLine(new Vector2(Rand, mitteY), new Vector2(Size.X - Rand, mitteY), linie, 2);
        DrawArc(new Vector2(Size.X / 2f, mitteY), Size.X * 0.11f, 0, Mathf.Pi * 2, 64, linie, 2, true);
        DrawCircle(new Vector2(Size.X / 2f, mitteY), 2.5f, linie);

        // Strafräume und Torräume, oben und unten
        float b16 = Size.X * 0.56f, t16 = Size.Y * 0.15f;
        float b5 = Size.X * 0.28f, t5 = Size.Y * 0.06f;
        DrawRect(new Rect2((Size.X - b16) / 2f, Rand, b16, t16), linie, false, 2);
        DrawRect(new Rect2((Size.X - b5) / 2f, Rand, b5, t5), linie, false, 2);
        DrawRect(new Rect2((Size.X - b16) / 2f, Size.Y - Rand - t16, b16, t16), linie, false, 2);
        DrawRect(new Rect2((Size.X - b5) / 2f, Size.Y - Rand - t5, b5, t5), linie, false, 2);
    }
}

// ---------------------------------------------------------------------------
// Main view
// ---------------------------------------------------------------------------
public partial class AufstellungView : Control
{
    private const string EinstellungAmateureAnzeigen = "aufstellung_amateure_anzeigen";

    // Spielfeld - passt samt zweireihiger Bank ins Fenster mit 900 Pixeln Höhe.
    private const float FieldW = 430f;
    private const float FieldH = 500f;
    private const float SlotW  = PositionSlot.PlaketteBreite;
    private const float SlotH  = PositionSlot.PlaketteHoehe;

    /// <summary>Die Formation, wie der Server sie aus den Plätzen liest - "4-2-3-1" oder "3-3-3-1".</summary>
    private Label _formationsLabel = null!;
    private OptionButton _vorlagenAuswahl = null!;
    private List<FormationsVorlage> _vorlagen = new();

    /// <summary>Wo jeder Platz liegt, normiert wie in der Engine: X vom eigenen Tor, Y von links.</summary>
    private readonly Dictionary<string, Vector2> _plaetze = new();
    private readonly Dictionary<string, Button> _filterKnoepfe = new();
    private string _filter = "Alle";
    private Control         _fieldControl = null!;
    private Symbol          _warnungSymbol = null!;
    private FmGrid<Spieler> _spielerGrid  = null!;
    private CheckBox        _amateureCheckbox = null!;
    private Label           _statusLabel = null!;
    private Label         _staerkeLabel = null!;
    private Label         _warnungLabel = null!;
    private Button        _autoBtn = null!;

    private const int BankSpalten = 4;

    private Label         _bankLabel     = null!;
    private Label         _bankHinweis   = null!;
    private GridContainer _bankContainer = null!;
    private readonly List<PositionSlot> _bankSlots = new();

    /// <summary>Wer gerade wo steht - speist die Spalte "Aufst." der Spielerliste.</summary>
    private readonly Dictionary<long, string> _belegung = new();

    private string _currentFormation = "4-4-2";
    private readonly Dictionary<string, PositionSlot> _slots = new();
    private readonly Dictionary<long, string>         _playerNames = new();
    private List<Spieler> _alleSpieler = new();

    public override async void _Ready()
    {
        BuildUI();
        await LadeAlles();
    }

    // ── UI-Aufbau ────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var root = new VBoxContainer();
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 16);
        AddChild(root);

        root.AddChild(BuildHeader());

        var content = new HBoxContainer();
        content.SizeFlagsVertical = SizeFlags.ExpandFill;
        content.AddThemeConstantOverride("separation", 24);
        root.AddChild(content);

        content.AddChild(BuildField());
        content.AddChild(BuildPlayerList());
    }

    private Control BuildHeader()
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 16);

        var titel = new VBoxContainer();
        titel.AddThemeConstantOverride("separation", 2);
        titel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titel.AddChild(FmTheme.MakeTitel("Aufstellung"));

        var warnung = new HBoxContainer();
        warnung.AddThemeConstantOverride("separation", 8);
        _warnungSymbol = Symbol.Neu(Symbol.Art.Haken, FmTheme.Success, 16);
        _warnungSymbol.Visible = false;
        warnung.AddChild(_warnungSymbol);
        _warnungLabel = FmTheme.MakeLabel("", 13, FmTheme.TextSecondary);
        warnung.AddChild(_warnungLabel);
        titel.AddChild(warnung);
        hbox.AddChild(titel);

        _statusLabel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        _statusLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        hbox.AddChild(_statusLabel);

        // Die Formation ergibt sich aus den Plätzen; die Vorlagen sind nur Startpunkte.
        var formation = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        formation.AddThemeConstantOverride("separation", -4);
        formation.AddChild(FmTheme.MakeLabel("FORMATION", 10, FmTheme.TextSecondary));
        _formationsLabel = FmTheme.MakeZahl("–", 30);
        _formationsLabel.TooltipText = "Ergibt sich daraus, wo die Spieler stehen. "
                                     + "Ziehe sie auf dem Rasen an eine andere Stelle.";
        _formationsLabel.MouseFilter = MouseFilterEnum.Pass;
        formation.AddChild(_formationsLabel);
        hbox.AddChild(formation);

        _vorlagenAuswahl = new OptionButton
        {
            CustomMinimumSize = new Vector2(190, 44),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = "Stellt die Plätze nach einer Vorlage auf - jeder Spieler wandert auf den "
                        + "nächstgelegenen Platz.",
        };
        _vorlagenAuswahl.ItemSelected += OnVorlageGewaehlt;
        hbox.AddChild(_vorlagenAuswahl);

        _autoBtn = new Button
        {
            Text = "Automatisch aufstellen",
            CustomMinimumSize = new Vector2(0, 44),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        FmTheme.ApplySecondaryButton(_autoBtn);
        foreach (var zustand in new[] { "normal", "hover", "pressed", "disabled" })
        {
            var stil = (StyleBoxFlat)_autoBtn.GetThemeStylebox(zustand).Duplicate();
            stil.ContentMarginLeft = 42;
            _autoBtn.AddThemeStyleboxOverride(zustand, stil);
        }
        var blitz = Symbol.Neu(Symbol.Art.Blitz, FmTheme.Accent, 18);
        blitz.Position = new Vector2(16, 13);
        blitz.Size = new Vector2(18, 18);
        _autoBtn.AddChild(blitz);
        _autoBtn.TooltipText = "Besetzt jeden Platz der aktuellen Formation mit dem stärksten "
                             + "verfügbaren Spieler und füllt die Ersatzbank.";
        _autoBtn.Pressed += async () => await AutomatischAufstellen();
        hbox.AddChild(_autoBtn);

        // Kein Speichern-Knopf: Jede Änderung wird sofort übernommen, gespielt wird immer
        // mit der aktuellen Aufstellung.
        return hbox;
    }

    /// <summary>Zeigt die Formation und - wenn sie einer Vorlage entspricht - diese in der Auswahl.</summary>
    private void ZeigeFormationsAuswahl()
    {
        _formationsLabel.Text = string.IsNullOrEmpty(_currentFormation) ? "–" : _currentFormation;
        _vorlagenAuswahl.Clear();
        _vorlagenAuswahl.AddItem("Vorlage wählen …");
        _vorlagenAuswahl.SetItemDisabled(0, true);
        int gewaehlt = 0;
        for (int i = 0; i < _vorlagen.Count; i++)
        {
            _vorlagenAuswahl.AddItem(_vorlagen[i].Name);
            if (_vorlagen[i].Name == _currentFormation) gewaehlt = i + 1;
        }
        _vorlagenAuswahl.Select(gewaehlt);
    }

    /// <summary>Zeigt an, ob die Mannschaft so antreten kann.</summary>
    private void AktualisiereWarnung(AufstellungModel? aufstellung)
    {
        if (aufstellung == null)
        {
            _warnungLabel.Text = "";
            return;
        }

        var warnung = aufstellung.Warnung;
        _warnungSymbol.Visible = true;
        if (warnung == null)
        {
            int bank = _bankSlots.Count(b => b.SpielerId.HasValue);
            _warnungLabel.Text = $"Spielbereit · 11 von 11 aufgestellt · Bank {bank} von {_bankSlots.Count}";
            _warnungLabel.AddThemeColorOverride("font_color", FmTheme.Success);
            _warnungSymbol.Form = Symbol.Art.Haken;
            _warnungSymbol.Farbe = FmTheme.Success;
            return;
        }

        // Zu wenige Spieler verhindern den Anpfiff, fehlende Positionen sind nur ein Hinweis.
        _warnungLabel.Text = warnung;
        var farbe = aufstellung.Spielbereit ? FmTheme.Gold : FmTheme.Danger;
        _warnungLabel.AddThemeColorOverride("font_color", farbe);
        _warnungSymbol.Form = aufstellung.Spielbereit ? Symbol.Art.Warnung : Symbol.Art.Kreuz;
        _warnungSymbol.Farbe = farbe;
    }

    private Control BuildField()
    {
        var spalte = new VBoxContainer { CustomMinimumSize = new Vector2(FieldW, 0) };
        spalte.AddThemeConstantOverride("separation", 10);

        _fieldControl = new Rasen { CustomMinimumSize = new Vector2(FieldW, FieldH) };
        spalte.AddChild(_fieldControl);

        // Teamstärke als Tafel unten links auf dem Rasen
        var tafel = new PanelContainer { Position = new Vector2(22, FieldH - 76) };
        var tafelStil = FmTheme.Flaeche(new Color(0.035f, 0.055f, 0.047f, 0.85f), 10);
        tafelStil.ContentMarginLeft = tafelStil.ContentMarginRight = 12;
        tafelStil.ContentMarginTop = tafelStil.ContentMarginBottom = 6;
        tafel.AddThemeStyleboxOverride("panel", tafelStil);
        tafel.TooltipText =
            "Durchschnitt der Stärke über alle Positionen der Formation.\n"
          + "Unbesetzte Positionen zählen dabei mit 0 - sonst wäre eine Elf mit neun\n"
          + "starken Spielern rechnerisch besser als eine vollständige mit denselben\n"
          + "neun plus zwei schwächeren.";
        var tafelInhalt = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        tafelInhalt.AddThemeConstantOverride("separation", -4);
        var tafelTitel = FmTheme.MakeLabel("TEAMSTÄRKE", 10, FmTheme.TextSecondary);
        tafelTitel.AddThemeFontOverride("font", FmTheme.SchriftUi(600));
        tafelInhalt.AddChild(tafelTitel);
        _staerkeLabel = FmTheme.MakeZahl("–", 28);
        tafelInhalt.AddChild(_staerkeLabel);
        tafel.AddChild(tafelInhalt);
        _fieldControl.AddChild(tafel);

        var bankKopf = new HBoxContainer();
        _bankLabel = FmTheme.MakeAbschnitt("Ersatzbank", 20);
        _bankLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bankKopf.AddChild(_bankLabel);
        _bankHinweis = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        _bankHinweis.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        bankKopf.AddChild(_bankHinweis);
        spalte.AddChild(bankKopf);

        // Die Anzahl der Plaetze kommt vom Server (pro Wettbewerb), daher erst beim Laden gefuellt.
        _bankContainer = new GridContainer { Columns = BankSpalten };
        _bankContainer.AddThemeConstantOverride("h_separation", 8);
        _bankContainer.AddThemeConstantOverride("v_separation", 8);
        spalte.AddChild(_bankContainer);

        return spalte;
    }

    /// <summary>
    /// Ein Platz der Engine auf dem Rasen: das eigene Tor unten, die Seite von links nach rechts.
    /// Leicht gestaucht, damit auch ein Platz an der Seitenlinie noch ganz auf dem Rasen steht.
    /// </summary>
    private static Vector2 AufRasen(Vector2 platz) => new(
        FieldW * (0.5f + (platz.Y - 0.5f) * 0.95f),
        FieldH * (0.93f - 0.9f * platz.X));

    /// <summary>Die Umkehrung von <see cref="AufRasen"/>, auf die erlaubten Grenzen geklemmt.</summary>
    private static Vector2 VomRasen(Vector2 mitte) => new(
        Mathf.Clamp((0.93f - mitte.Y / FieldH) / 0.9f, (float)Platzierung.XMin, (float)Platzierung.XMax),
        Mathf.Clamp(0.5f + (mitte.X / FieldW - 0.5f) / 0.95f, (float)Platzierung.YMin, (float)Platzierung.YMax));

    private void BuildSlots()
    {
        foreach (var slot in _slots.Values)
            slot.QueueFree();
        _slots.Clear();

        var alleSlots = _plaetze.Keys.ToList();
        foreach (var (slotName, platz) in _plaetze)
        {
            var slot = PositionSlot.Create(slotName, SlotBezeichnung.Fuer(slotName, alleSlots));
            var mitte = AufRasen(platz);
            slot.Position = new Vector2(
                Mathf.Clamp(mitte.X - SlotW * 0.5f, 0, FieldW - SlotW),
                Mathf.Clamp(mitte.Y - SlotH * 0.5f, 0, FieldH - SlotH));
            slot.Size = new Vector2(SlotW, SlotH);
            slot.Verschiebbar = SlotBezeichnung.Basis(slotName) != "TW";

            slot.PlayerDropped += OnPlayerDropped;
            slot.Pressed       += () => OnSlotPressed(slot);
            slot.Zieht         += (s, m) => { var p = VomRasen(m); s.ZeigeVorschau(Platzierung.PositionAus(p.X, p.Y)); };
            slot.Abgelegt      += OnPlatzVerschoben;
            _slots[slotName] = slot;
            _fieldControl.AddChild(slot);
        }

        // Die Teamstärke-Tafel liegt unter den Plaketten - im Strafraum vor dem eigenen Tor
        // steht sie sonst über dem Torwart.
        var tafel = _staerkeLabel?.GetParent()?.GetParent() as Control;
        if (tafel?.GetParent() == _fieldControl)
            _fieldControl.MoveChild(tafel, 0);
    }

    /// <summary>Legt so viele Bankplätze an, wie der Wettbewerb zulässt.</summary>
    private void BaueBank(int plaetze)
    {
        foreach (var slot in _bankSlots)
            slot.QueueFree();
        _bankSlots.Clear();

        foreach (Node child in _bankContainer.GetChildren())
            child.QueueFree();

        _bankHinweis.Text = plaetze > 0
            ? $"{plaetze} Plätze"
            : "in diesem Wettbewerb nicht vorgesehen";

        for (int i = 0; i < plaetze; i++)
        {
            var slot = PositionSlot.Create($"B{i + 1}", $"B{i + 1}", bank: true);
            slot.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            slot.PlayerDropped += OnPlayerDropped;
            slot.Pressed       += () => OnSlotPressed(slot);
            _bankSlots.Add(slot);
            _bankContainer.AddChild(slot);
        }
    }

    private Control BuildPlayerList()
    {
        var panel = new PanelContainer();
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var style = FmTheme.Flaeche(FmTheme.BgPanel, 14, FmTheme.Border);
        style.SetContentMarginAll(1);
        panel.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);
        panel.AddChild(vbox);

        var kopfrand = new MarginContainer();
        FmTheme.SetMargin(kopfrand, 16, 16, 14, 12);
        vbox.AddChild(kopfrand);

        var kopf = new VBoxContainer();
        kopf.AddThemeConstantOverride("separation", 10);
        kopfrand.AddChild(kopf);

        var titelzeile = new HBoxContainer();
        var titel = FmTheme.MakeAbschnitt("Kader", 20);
        titel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titelzeile.AddChild(titel);
        var hinweis = FmTheme.MakeLabel("Auf einen Platz ziehen · Plätze auf dem Rasen verschieben · Rechtsklick entfernt", 12, FmTheme.TextSecondary);
        hinweis.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        titelzeile.AddChild(hinweis);
        kopf.AddChild(titelzeile);

        var kopfleiste = new HBoxContainer();
        kopfleiste.AddThemeConstantOverride("separation", 6);
        kopf.AddChild(kopfleiste);

        // Filter nach Mannschaftsteil
        foreach (var name in new[] { "Alle", "Tor", "Abwehr", "Mittelfeld", "Sturm" })
        {
            var knopf = new Button { Text = name, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 32) };
            var n = name;
            knopf.Pressed += () =>
            {
                _filter = n;
                ZeigeFilterAuswahl();
                FuelleSpielerListe(_alleSpieler);
            };
            _filterKnoepfe[name] = knopf;
            kopfleiste.AddChild(knopf);
        }
        ZeigeFilterAuswahl();

        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        kopfleiste.AddChild(spacer);

        _amateureCheckbox = new CheckBox
        {
            Text = "+ Amateure",
            ButtonPressed = ClientEinstellungen.GetBool(EinstellungAmateureAnzeigen, true),
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 32),
        };
        // Als Chip wie die Filter daneben - ein Kästchen ist auf dem dunklen Grund kaum zu sehen.
        var leer = ImageTexture.CreateFromImage(Image.CreateEmpty(1, 1, false, Image.Format.Rgba8));
        foreach (var symbol in new[] { "checked", "unchecked", "checked_disabled", "unchecked_disabled" })
            _amateureCheckbox.AddThemeIconOverride(symbol, leer);
        var aus = FmTheme.Flaeche(new Color(0, 0, 0, 0), 16, FmTheme.BorderStark);
        aus.ContentMarginLeft = aus.ContentMarginRight = 12;
        var an = FmTheme.Flaeche(FmTheme.BgHover, 16, FmTheme.TextGedimmt);
        an.ContentMarginLeft = an.ContentMarginRight = 12;
        _amateureCheckbox.AddThemeStyleboxOverride("normal", aus);
        _amateureCheckbox.AddThemeStyleboxOverride("hover", an);
        _amateureCheckbox.AddThemeStyleboxOverride("pressed", an);
        _amateureCheckbox.AddThemeStyleboxOverride("hover_pressed", an);
        _amateureCheckbox.AddThemeColorOverride("font_color", FmTheme.TextNav);
        _amateureCheckbox.AddThemeColorOverride("font_pressed_color", FmTheme.TextPrimary);
        _amateureCheckbox.AddThemeColorOverride("font_hover_pressed_color", FmTheme.TextPrimary);
        _amateureCheckbox.AddThemeFontOverride("font", FmTheme.SchriftUi(600));
        _amateureCheckbox.AddThemeFontSizeOverride("font_size", 12);
        _amateureCheckbox.TooltipText = "Blendet Spieler aus dem Amateurkader in dieser Liste "
                                       + "ein oder aus - bereits aufgestellte Amateure bleiben "
                                       + "im Feld stehen. Die Einstellung wird gemerkt.";
        _amateureCheckbox.Toggled += gezeigt =>
        {
            ClientEinstellungen.SetBool(EinstellungAmateureAnzeigen, gezeigt);
            FuelleSpielerListe(_alleSpieler);
        };
        kopfleiste.AddChild(_amateureCheckbox);

        // Spalten, Farben, Mouseover und das Ziehen kommen aus dem gemeinsamen Grid.
        var spalten = new List<GridSpalte<Spieler>> { AufstellungsSpalte };
        spalten.AddRange(SpielerSpalten.Aufstellungsliste);

        _spielerGrid = new FmGrid<Spieler>(spalten)
        {
            SizeFlagsVertical  = SizeFlags.ExpandFill,
            Zeilenfarbe        = SpielerSpalten.Zeilenfarbe,
            ZebraNeuBei        = s => s.Gruppe,
            Standardsortierung = SpielerSpalten.NachAufstellung,
            ZiehDaten = s => new Godot.Collections.Dictionary
            {
                ["id"]       = s.Id,
                ["name"]     = s.Name,
                ["position"] = s.Position,
            },
        };
        vbox.AddChild(_spielerGrid);

        return panel;
    }

    /// <summary>Filterchips: der gewählte hell, die übrigen nur umrandet.</summary>
    private void ZeigeFilterAuswahl()
    {
        foreach (var (name, knopf) in _filterKnoepfe)
        {
            bool aktiv = name == _filter;
            var stil = FmTheme.Flaeche(aktiv ? FmTheme.TextPrimary : new Color(0, 0, 0, 0), 16,
                aktiv ? FmTheme.TextPrimary : FmTheme.BorderStark);
            stil.ContentMarginLeft = stil.ContentMarginRight = 12;
            var schwebend = (StyleBoxFlat)stil.Duplicate();
            if (!aktiv) schwebend.BgColor = FmTheme.BgHover;
            foreach (var zustand in new[] { "normal", "pressed", "focus" })
                knopf.AddThemeStyleboxOverride(zustand, stil);
            knopf.AddThemeStyleboxOverride("hover", schwebend);
            var farbe = aktiv ? FmTheme.BgDark : FmTheme.TextNav;
            knopf.AddThemeColorOverride("font_color", farbe);
            knopf.AddThemeColorOverride("font_pressed_color", farbe);
            knopf.AddThemeColorOverride("font_hover_color", aktiv ? FmTheme.BgDark : FmTheme.TextPrimary);
            knopf.AddThemeFontOverride("font", FmTheme.SchriftUi(600));
            knopf.AddThemeFontSizeOverride("font_size", 12);
        }
    }

    /// <summary>Auf welchem Platz der Spieler gerade steht - Feldposition oder Bank.</summary>
    private GridSpalte<Spieler> AufstellungsSpalte => new()
    {
        Titel = "Aufst.", Breite = 58, Ausrichtung = HorizontalAlignment.Center,
        Text    = s => _belegung.GetValueOrDefault(s.Id) ?? "–",
        Farbe   = s => _belegung.ContainsKey(s.Id) ? FmTheme.Accent : FmTheme.TextSecondary,
        Tooltip = s => _belegung.TryGetValue(s.Id, out var slot)
            ? $"Steht auf {slot}"
            : "Nicht aufgestellt - auf eine Position im Feld ziehen.",
    };

    // ── Daten laden ──────────────────────────────────────────────────────────

    private async System.Threading.Tasks.Task LadeAlles()
    {
        _statusLabel.Text = "Lade …";
        var vereinId = GameState.Instance.VereinId;

        var spielerTask     = ApiClient.GetAsync<List<Spieler>>($"verein/{vereinId}/spieler");
        var aufstellungTask = ApiClient.GetAsync<AufstellungModel>($"aufstellung/{vereinId}");
        var vorlagenTask    = ApiClient.GetAsync<List<FormationsVorlage>>("aufstellung/vorlagen");

        _alleSpieler = await spielerTask ?? new List<Spieler>();

        foreach (var s in _alleSpieler)
            _playerNames[s.Id] = s.Name;

        FuelleSpielerListe(_alleSpieler);

        var aufstellung = await aufstellungTask;
        _vorlagen = await vorlagenTask ?? new List<FormationsVorlage>();

        // Auch ohne gespeicherte Aufstellung liefert der Server die Bankgröße des Wettbewerbs.
        BaueBank(aufstellung?.MaxErsatzbank ?? 0);

        if (aufstellung != null && aufstellung.Koordinaten.Count > 0)
        {
            UebernimmPlaetze(aufstellung);
            WendeSpielerZuweisungenAn(aufstellung.Positionen, aufstellung.SlotStaerken,
                aufstellung.SlotGrundstaerken, aufstellung.SlotEingespieltheit);
            AktualisiereSaerkeLabel(aufstellung.Gesamtstaerke);
        }
        else
        {
            // Noch nie aufgestellt: die erste Vorlage als Ausgangspunkt.
            SetzeVorlage(_vorlagen.FirstOrDefault());
        }
        if (aufstellung != null)
        {
            WendeBankAn(aufstellung.Ersatzbank);
        }
        // Erst jetzt eintragen: Die Liste entsteht, bevor die Aufstellung geladen ist.
        AktualisiereListenPositionen();
        AktualisiereWarnung(aufstellung);

        int kader = _alleSpieler.Count(s => s.Kader == "Profi" || s.Kader == "Amateur");
        _statusLabel.Text = $"{kader} Spieler in Profi- und Amateurkader";
    }

    private void FuelleSpielerListe(List<Spieler> spieler)
    {
        // Nur Profi- und Amateurkader; Jugendspieler stehen hier nicht zur Wahl. Der Amateurkader
        // laesst sich zusaetzlich per Checkbox ausblenden - wer dort bereits steht, bleibt im
        // Feld, die Checkbox filtert nur die Auswahlliste.
        bool amateureZeigen = _amateureCheckbox == null || _amateureCheckbox.ButtonPressed;
        _spielerGrid.Zeige(spieler.Where(s =>
            (s.Kader == "Profi" || (s.Kader == "Amateur" && amateureZeigen))
            && PasstZumFilter(s)));
        AktualisiereListenPositionen();
    }

    /// <summary>Ob ein Spieler zum gewählten Mannschaftsteil passt.</summary>
    private bool PasstZumFilter(Spieler s) => _filter switch
    {
        "Tor"        => s.Gruppe == Positionsgruppe.Tor,
        "Abwehr"     => s.Gruppe == Positionsgruppe.Abwehr,
        "Mittelfeld" => s.Gruppe == Positionsgruppe.Mittelfeld,
        "Sturm"      => s.Gruppe == Positionsgruppe.Sturm,
        _            => true,
    };

    /// <summary>
    /// Traegt in die Liste ein, wer gerade wo steht. Aktualisiert nur die Zellen, statt die
    /// Liste neu aufzubauen - so bleibt die Scrollposition erhalten.
    /// </summary>
    private void AktualisiereListenPositionen()
    {
        _belegung.Clear();

        foreach (var (_, slot) in _slots)
        {
            if (slot.SpielerId.HasValue)
                _belegung[slot.SpielerId.Value] = slot.Anzeige;
        }
        foreach (var slot in _bankSlots)
        {
            if (slot.SpielerId.HasValue)
                _belegung[slot.SpielerId.Value] = slot.Anzeige;
        }

        _spielerGrid.Aktualisiere();
    }

    /// <summary>Lässt den Server die stärkste Elf und die Ersatzbank bestimmen.</summary>
    private async System.Threading.Tasks.Task AutomatischAufstellen()
    {
        _autoBtn.Disabled = true;
        _statusLabel.Text = "Stelle auf …";

        var vereinId = GameState.Instance.VereinId;
        var ergebnis = await ApiClient.PostAsync<object, AufstellungModel>(
            $"aufstellung/{vereinId}/automatisch", new { });

        _autoBtn.Disabled = false;

        if (ergebnis == null)
        {
            _statusLabel.Text = "Automatische Aufstellung fehlgeschlagen";
            return;
        }

        WendeAufstellungAn(ergebnis);
        _statusLabel.Text = "Automatisch aufgestellt";
    }

    /// <summary>Überträgt eine komplette Aufstellung vom Server auf die Oberfläche.</summary>
    private void WendeAufstellungAn(AufstellungModel aufstellung)
    {
        // Neu aufbauen, damit alte Zuweisungen nicht stehen bleiben - und weil der Server die
        // Plätze nach dem Verschieben neu benannt hat.
        UebernimmPlaetze(aufstellung);
        foreach (var slot in _bankSlots)
            slot.Clear();

        WendeSpielerZuweisungenAn(aufstellung.Positionen, aufstellung.SlotStaerken,
            aufstellung.SlotGrundstaerken, aufstellung.SlotEingespieltheit);
        WendeBankAn(aufstellung.Ersatzbank);
        AktualisiereListenPositionen();
        AktualisiereWarnung(aufstellung);
        AktualisiereSaerkeLabel(aufstellung.Gesamtstaerke);
    }

    /// <summary>Übernimmt Plätze und Formation vom Server und baut die Plaketten neu.</summary>
    private void UebernimmPlaetze(AufstellungModel aufstellung)
    {
        _plaetze.Clear();
        foreach (var (slot, k) in aufstellung.Koordinaten)
            _plaetze[slot] = new Vector2((float)k.X, (float)k.Y);
        _currentFormation = aufstellung.Formation ?? "";
        ZeigeFormationsAuswahl();
        BuildSlots();
    }

    /// <summary>Setzt die Plätze einer Vorlage, ohne Spieler.</summary>
    private void SetzeVorlage(FormationsVorlage? vorlage)
    {
        if (vorlage == null) return;
        _plaetze.Clear();
        foreach (var p in vorlage.Plaetze)
            _plaetze[p.Slot] = new Vector2((float)p.X, (float)p.Y);
        _currentFormation = vorlage.Name;
        ZeigeFormationsAuswahl();
        BuildSlots();
    }

    private void WendeBankAn(List<long>? ersatzbank)
    {
        if (ersatzbank == null) return;
        for (int i = 0; i < _bankSlots.Count && i < ersatzbank.Count; i++)
        {
            if (_playerNames.TryGetValue(ersatzbank[i], out var name))
            {
                var spieler = _alleSpieler.Find(s => s.Id == ersatzbank[i]);
                _bankSlots[i].Assign(ersatzbank[i], name, spieler?.Staerke,
                    spielerPosition: spieler?.Position);
            }
        }
    }

    private void WendeSpielerZuweisungenAn(Dictionary<string, long>? positionen,
        Dictionary<string, int>? slotStaerken = null,
        Dictionary<string, int>? slotGrundstaerken = null,
        Dictionary<string, int>? slotEingespieltheit = null)
    {
        if (positionen == null) return;
        foreach (var (slot, spielerId) in positionen)
        {
            if (_slots.TryGetValue(slot, out var slotCtrl) &&
                _playerNames.TryGetValue(spielerId, out var name))
            {
                int? slotStaerke = null;
                if (slotStaerken != null && slotStaerken.TryGetValue(slot, out var st))
                    slotStaerke = st;
                int? grundstaerke = null;
                if (slotGrundstaerken != null && slotGrundstaerken.TryGetValue(slot, out var g))
                    grundstaerke = g;
                int? eingespieltheit = null;
                if (slotEingespieltheit != null && slotEingespieltheit.TryGetValue(slot, out var e))
                    eingespieltheit = e;
                slotCtrl.Assign(spielerId, name, slotStaerke, grundstaerke, eingespieltheit);
            }
        }
    }

    // ── Event-Handler ────────────────────────────────────────────────────────

    /// <summary>
    /// Stellt die Plätze nach einer Vorlage auf. Wer schon aufgestellt ist, bleibt es: Jeder wandert
    /// auf einen nahen freien Platz der neuen Formation, bevorzugt auf seine gelernte Position; der
    /// Torwart bleibt im Tor. Nur nach Nähe zu gehen tauschte gern einen Innenverteidiger mit dem
    /// Sechser, der zufällig näher an der neuen Innenverteidigerstelle stand.
    /// </summary>
    private void OnVorlageGewaehlt(long index)
    {
        if (index <= 0 || index > _vorlagen.Count) return;
        var vorlage = _vorlagen[(int)index - 1];

        var aufgestellt = _slots
            .Where(kv => kv.Value.SpielerId.HasValue)
            .Select(kv => (Slot: kv.Key, Platz: _plaetze[kv.Key], Spieler: kv.Value.SpielerId!.Value,
                Name: kv.Value.SpielerName))
            .ToList();

        SetzeVorlage(vorlage);

        // Gierig: das jeweils beste Paar aus Spieler und freiem Platz zuerst - nah und, wenn
        // möglich, auf seiner gelernten Position.
        const float FremdePosition = 0.2f;
        var frei = new HashSet<string>(_plaetze.Keys);
        var paare = new List<(float Abstand, int Spieler, string Platz)>();
        for (int i = 0; i < aufgestellt.Count; i++)
        {
            bool torwart = SlotBezeichnung.Basis(aufgestellt[i].Slot) == "TW";
            var gelernt = _alleSpieler.Find(sp => sp.Id == aufgestellt[i].Spieler)?.Position;
            foreach (var (slot, platz) in _plaetze)
            {
                if ((SlotBezeichnung.Basis(slot) == "TW") != torwart) continue;
                float kosten = aufgestellt[i].Platz.DistanceTo(platz)
                             + (SlotBezeichnung.Basis(slot) == gelernt ? 0 : FremdePosition);
                paare.Add((kosten, i, slot));
            }
        }
        var kostenVon = paare.ToDictionary(p => (p.Spieler, p.Platz), p => p.Abstand);
        var zuteilung = new Dictionary<int, string>();
        foreach (var (_, i, slot) in paare.OrderBy(p => p.Abstand))
        {
            if (zuteilung.ContainsKey(i) || !frei.Contains(slot)) continue;
            zuteilung[i] = slot;
            frei.Remove(slot);
        }

        // Gierig bleibt für den Letzten oft nur ein Platz am anderen Ende - der Linksverteidiger
        // landete so im Sturm. Darum nachbessern: Zwei tauschen, solange sie zusammen besser stehen.
        float Kosten(int spieler, string platz) =>
            kostenVon.TryGetValue((spieler, platz), out var k) ? k : float.MaxValue;
        bool besser = true;
        while (besser)
        {
            besser = false;
            var spieler = zuteilung.Keys.ToList();
            foreach (var a in spieler)
            foreach (var b in spieler)
            {
                if (a >= b) continue;
                string pa = zuteilung[a], pb = zuteilung[b];
                if (Kosten(a, pb) + Kosten(b, pa) < Kosten(a, pa) + Kosten(b, pb) - 1e-4f)
                {
                    zuteilung[a] = pb;
                    zuteilung[b] = pa;
                    besser = true;
                }
            }
        }
        foreach (var (i, slot) in zuteilung)
            _slots[slot].Assign(aufgestellt[i].Spieler, aufgestellt[i].Name);

        AktualisiereListenPositionen();
        _ = UebernehmeAenderung();
        _statusLabel.Text = $"Vorlage {vorlage.Name}";
    }

    /// <summary>
    /// Für den Vorschaumodus: zieht eine Plakette mit echten Mausereignissen an eine Stelle - so
    /// lässt sich das Verschieben ohne Hand an der Maus prüfen.
    /// </summary>
    internal async System.Threading.Tasks.Task VorschauZiehen(string slot, Vector2 ziel)
    {
        if (!_slots.TryGetValue(slot, out var plakette)) { GD.PrintErr($"[Vorschau] Platz {slot} fehlt."); return; }
        var start = plakette.GetGlobalRect().GetCenter();
        var ende = _fieldControl.GetGlobalTransform() * AufRasen(ziel);
        var vp = GetViewport();
        vp.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true,
            Position = start, GlobalPosition = start });
        for (int i = 1; i <= 12; i++)
        {
            var p = start.Lerp(ende, i / 12f);
            vp.PushInput(new InputEventMouseMotion { Position = p, GlobalPosition = p,
                ButtonMask = MouseButtonMask.Left });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        vp.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false,
            Position = ende, GlobalPosition = ende });
    }

    /// <summary>Für den Vorschaumodus: wählt eine Vorlage, als hätte der Nutzer sie ausgesucht.</summary>
    internal void VorschauVorlage(string name)
    {
        int index = _vorlagen.FindIndex(v => v.Name == name);
        if (index < 0) { GD.PrintErr($"[Vorschau] Vorlage {name} fehlt."); return; }
        OnVorlageGewaehlt(index + 1);
    }

    /// <summary>Ein Platz wurde verschoben: neue Stelle merken und übernehmen.</summary>
    private void OnPlatzVerschoben(PositionSlot slot)
    {
        var mitte = slot.Position + slot.Size / 2;
        _plaetze[slot.SlotName] = VomRasen(mitte);
        _ = UebernehmeAenderung();
    }

    private void OnPlayerDropped(PositionSlot targetSlot, long spielerId, string spielerName)
    {
        // Prüfen ob Spieler bereits auf einem anderen Slot ist -> dort entfernen.
        // Gilt auch für die Bank: Niemand steht gleichzeitig im Feld und auf der Bank.
        foreach (var slot in _slots.Values)
        {
            if (slot != targetSlot && slot.SpielerId == spielerId)
                slot.Clear();
        }
        foreach (var slot in _bankSlots)
        {
            if (slot != targetSlot && slot.SpielerId == spielerId)
                slot.Clear();
        }

        // Steht auf dem Ziel-Slot schon jemand anderes, wird er einfach überschrieben -
        // ein Tausch zurück ist nicht nötig, der vorherige landet ja bereits leer da oben.
        var gezogen = _alleSpieler.Find(s => s.Id == spielerId);
        targetSlot.Assign(spielerId, spielerName,
            targetSlot.IstBank ? gezogen?.Staerke : null,
            spielerPosition: gezogen?.Position);
        AktualisiereListenPositionen();
        _statusLabel.Text = $"{spielerName} auf {targetSlot.Anzeige}";

        // Auto-Speichern nach Zuweisung
        _ = UebernehmeAenderung();
    }

    private void OnSlotPressed(PositionSlot slot)
    {
        // Rechtsklick / Pressed: Slot leeren
        if (slot.SpielerId.HasValue)
        {
            slot.Clear();
            AktualisiereListenPositionen();
            _ = UebernehmeAenderung();
        }
    }

    /// <summary>Überträgt die aktuelle Aufstellung sofort zum Server.</summary>
    private async System.Threading.Tasks.Task UebernehmeAenderung()
    {
        var vereinId = GameState.Instance.VereinId;
        var dto = new AufstellungModel
        {
            VereinId  = vereinId,
            Formation = _currentFormation,
            Koordinaten = _plaetze.ToDictionary(kv => kv.Key,
                kv => new PlatzKoordinate { X = kv.Value.X, Y = kv.Value.Y }),
            Positionen = _slots
                .Where(kv => kv.Value.SpielerId.HasValue)
                .ToDictionary(kv => kv.Key, kv => kv.Value.SpielerId!.Value),
            // Reihenfolge der Bankplätze bleibt erhalten; leere Plätze fallen heraus.
            Ersatzbank = _bankSlots
                .Where(s => s.SpielerId.HasValue)
                .Select(s => s.SpielerId!.Value)
                .ToList(),
        };

        var result = await ApiClient.PostAsync<AufstellungModel, AufstellungModel>(
            $"aufstellung/{vereinId}", dto);

        if (result != null)
        {
            // Der Server hat die Plätze aus ihrer Stelle neu benannt - also alles von ihm übernehmen.
            WendeAufstellungAn(result);
        }
        else
        {
            _statusLabel.Text = "Änderung konnte nicht übernommen werden";
        }
    }

    private void AktualisiereSaerkeLabel(double gesamtstaerke)
    {
        // Durchschnitt über alle Positionen der Formation - Kultur-unabhängig mit Punkt-Trennung
        // wäre irritierend, deshalb bewusst die deutsche Schreibweise.
        _staerkeLabel.Text = gesamtstaerke > 0
            ? gesamtstaerke.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))
            : "–";
    }

}
