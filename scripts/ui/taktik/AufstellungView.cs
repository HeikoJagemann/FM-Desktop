#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Taktik;

// ---------------------------------------------------------------------------
// Draggable player row in the right-hand list
// ---------------------------------------------------------------------------
public partial class PlayerRow : PanelContainer
{
    private const string NichtAufgestellt = "–";

    public Spieler Spieler { get; private set; } = null!;

    private Label _slotLabel = null!;

    /// <summary>Zeigt an, auf welchem Platz der Spieler gerade steht (Feld-Slot oder Bank).</summary>
    public void SetzeAufstellungsSlot(string? slot)
    {
        bool aufgestellt = !string.IsNullOrEmpty(slot);
        _slotLabel.Text = aufgestellt ? slot : NichtAufgestellt;
        _slotLabel.AddThemeColorOverride("font_color",
            aufgestellt ? FmTheme.Accent : FmTheme.TextSecondary);
    }

    /// <summary>Spaltenbreiten - identisch mit dem Kopf der Liste.</summary>
    internal static readonly (string Titel, int Breite, bool Expand, HorizontalAlignment Ausrichtung)[] Spalten =
    {
        ("Aufst.",   58, false, HorizontalAlignment.Center),
        ("Name",    140, true,  HorizontalAlignment.Left),
        ("Pos",      42, false, HorizontalAlignment.Left),
        ("Stärken", 150, true,  HorizontalAlignment.Left),
        ("Talent",   80, false, HorizontalAlignment.Left),
        ("Alter",    44, false, HorizontalAlignment.Center),
        ("Frische",  62, false, HorizontalAlignment.Center),
        ("Kader",    72, false, HorizontalAlignment.Left),
    };

    /// <summary>
    /// Frische in Prozent. Ein Wert von 0 heißt "nie gesetzt" - solche Spieler stammen aus
    /// Spielständen von vor dem Training und gehen mit voller Frische ins Spiel; eine "0 %" wäre
    /// hier schlicht falsch.
    /// </summary>
    private static string FrischeText(int frische) => frische > 0 ? $"{frische} %" : "–";

    private static Color FrischeFarbe(int frische) => frische switch
    {
        >= 85 => FmTheme.Success,
        >= 70 => FmTheme.TextPrimary,
        > 0   => FmTheme.Danger,
        _     => FmTheme.TextSecondary,
    };

    public static PlayerRow Create(Spieler s, bool abgesetzt)
    {
        var row = new PlayerRow { Spieler = s };
        row.CustomMinimumSize = new Vector2(0, 30);

        var style = new StyleBoxFlat { BgColor = FmTheme.FuerGruppe(s.Gruppe, abgesetzt) };
        style.SetContentMarginAll(0);
        row.AddThemeStyleboxOverride("panel", style);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 0);
        row.AddChild(hbox);

        int spalte = 0;
        Label AddCell(string text, Color? farbe = null)
        {
            var (_, breite, expand, ausrichtung) = Spalten[spalte++];
            var m = new MarginContainer();
            m.AddThemeConstantOverride("margin_left", 6);
            m.AddThemeConstantOverride("margin_right", 6);
            m.CustomMinimumSize = new Vector2(breite, 0);
            if (expand) m.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var lbl = new Label
            {
                Text = text,
                HorizontalAlignment = ausrichtung,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.Off,
            };
            lbl.AddThemeColorOverride("font_color", farbe ?? FmTheme.TextPrimary);
            lbl.AddThemeFontSizeOverride("font_size", 12);
            m.AddChild(lbl);
            hbox.AddChild(m);
            return lbl;
        }

        row._slotLabel = AddCell(NichtAufgestellt);
        AddCell(s.Name);
        AddCell(s.HauptPosition, FmTheme.TextFuerGruppe(s.Gruppe));
        AddCell(s.Top3PositionenText).TooltipText = s.Top3PositionenErklaerung;
        AddCell(TalentSterne(s.Talent), TalentFarbe(s.Talent));
        AddCell(s.Alter.ToString());
        AddCell(FrischeText(s.Frische), FrischeFarbe(s.Frische));
        AddCell(s.Kader, s.Kader == "Profi" ? FmTheme.TextPrimary : FmTheme.TextSecondary);

        row.MouseDefaultCursorShape = CursorShape.Drag;
        return row;
    }

    private static string TalentSterne(int talent)
    {
        int sterne = talent switch
        {
            >= 80 => 5, >= 65 => 4, >= 50 => 3, >= 35 => 2, _ => 1,
        };
        return new string('★', sterne) + new string('☆', 5 - sterne);
    }

    private static Color TalentFarbe(int talent) => talent switch
    {
        >= 80 => FmTheme.Gold,
        >= 65 => FmTheme.Success,
        _     => FmTheme.TextSecondary,
    };

    public override Variant _GetDragData(Vector2 atPosition)
    {
        var preview = new Label
        {
            Text = $"{Spieler.Position}  {Spieler.Name}",
        };
        preview.AddThemeColorOverride("font_color", FmTheme.TextPrimary);
        preview.AddThemeFontSizeOverride("font_size", 13);
        var bg = new PanelContainer();
        var style = new StyleBoxFlat { BgColor = FmTheme.BgPanel };
        style.SetBorderWidthAll(1);
        bg.AddThemeStyleboxOverride("panel", style);
        var m = new MarginContainer();
        FmTheme.SetMargin(m, 8, 4);
        m.AddChild(preview);
        bg.AddChild(m);
        SetDragPreview(bg);

        var data = new Godot.Collections.Dictionary
        {
            ["id"]       = Spieler.Id,
            ["name"]     = Spieler.Name,
            ["position"] = Spieler.Position,
        };
        return data;
    }
}

// ---------------------------------------------------------------------------
// Drop-target slot on the football field
// ---------------------------------------------------------------------------
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

    public event Action<PositionSlot, long, string>? PlayerDropped;
    public static PositionSlot Create(string slotName, string anzeige)
    {
        var slot = new PositionSlot { SlotName = slotName, Anzeige = anzeige };
        slot.CustomMinimumSize = new Vector2(64, 52);
        slot.Refresh();
        return slot;
    }

    public void Assign(long spielerId, string spielerName, int? staerke = null,
        int? grundstaerke = null, int? eingespieltheit = null)
    {
        SpielerId       = spielerId;
        SpielerName     = spielerName;
        Staerke         = staerke;
        Grundstaerke    = grundstaerke;
        Eingespieltheit = eingespieltheit;
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
        Refresh();
    }

    private void Refresh()
    {
        bool occupied = SpielerId.HasValue;
        var bgColor   = occupied ? new Color(0.15f, 0.45f, 0.25f) : new Color(0.10f, 0.30f, 0.15f);
        var style = new StyleBoxFlat
        {
            BgColor     = bgColor,
            BorderColor = occupied ? FmTheme.Success : FmTheme.Border,
            CornerRadiusTopLeft     = 6,
            CornerRadiusTopRight    = 6,
            CornerRadiusBottomLeft  = 6,
            CornerRadiusBottomRight = 6,
        };
        style.SetBorderWidthAll(1);
        style.SetContentMarginAll(2);
        AddThemeStyleboxOverride("normal",  style);
        AddThemeStyleboxOverride("hover",   MakeSlotStyle(bgColor.Lightened(0.1f), occupied));
        AddThemeStyleboxOverride("pressed", MakeSlotStyle(bgColor.Darkened(0.1f),  occupied));
        AddThemeStyleboxOverride("focus",   style);
        AddThemeColorOverride("font_color", FmTheme.TextPrimary);
        AddThemeFontSizeOverride("font_size", 11);

        // Auf dem Platz steht die Positionsbezeichnung, nicht der technische Schlüssel.
        if (occupied)
        {
            if (Staerke.HasValue)
                Text = $"{Anzeige}\n{TruncateName(SpielerName)}\n{Staerke.Value}";
            else
                Text = $"{Anzeige}\n{TruncateName(SpielerName)}";

            TooltipText = Staerke.HasValue && Grundstaerke.HasValue && Eingespieltheit.HasValue
                ? StaerkeErklaerung.Basis(Anzeige, Grundstaerke.Value, Eingespieltheit.Value, Staerke.Value)
                : "";
        }
        else
        {
            Text = Anzeige;
            TooltipText = "";
        }
    }

    private static StyleBoxFlat MakeSlotStyle(Color bg, bool occupied)
    {
        var s = new StyleBoxFlat
        {
            BgColor     = bg,
            BorderColor = occupied ? FmTheme.Success : FmTheme.Border,
            CornerRadiusTopLeft     = 6,
            CornerRadiusTopRight    = 6,
            CornerRadiusBottomLeft  = 6,
            CornerRadiusBottomRight = 6,
        };
        s.SetBorderWidthAll(1);
        s.SetContentMarginAll(2);
        return s;
    }

    private static string TruncateName(string name)
    {
        var parts = name.Split(' ');
        if (parts.Length == 1) return name.Length > 9 ? name[..9] + "." : name;
        return parts[^1].Length > 9 ? parts[^1][..9] + "." : parts[^1];
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

// ---------------------------------------------------------------------------
// Main view
// ---------------------------------------------------------------------------
public partial class AufstellungView : Control
{
    private static readonly string[] FormationNames = { "4-4-2", "4-2-3-1", "4-3-3" };

    private static readonly Dictionary<string, (string Slot, float X, float Y)[]> Formations = new()
    {
        ["4-4-2"] = new[]
        {
            ("TW",   0.50f, 0.88f),
            ("LV",   0.12f, 0.70f), ("IV_1", 0.37f, 0.70f), ("IV_2", 0.63f, 0.70f), ("RV", 0.88f, 0.70f),
            ("LM",   0.12f, 0.48f), ("ZM_1", 0.37f, 0.48f), ("ZM_2", 0.63f, 0.48f), ("RM", 0.88f, 0.48f),
            ("ST_1", 0.35f, 0.22f), ("ST_2", 0.65f, 0.22f),
        },
        ["4-2-3-1"] = new[]
        {
            ("TW",   0.50f, 0.88f),
            ("LV",   0.12f, 0.70f), ("IV_1", 0.37f, 0.70f), ("IV_2", 0.63f, 0.70f), ("RV", 0.88f, 0.70f),
            ("DM_1", 0.35f, 0.54f), ("DM_2", 0.65f, 0.54f),
            ("LM",   0.12f, 0.36f), ("OM",   0.50f, 0.36f), ("RM",   0.88f, 0.36f),
            ("ST",   0.50f, 0.16f),
        },
        ["4-3-3"] = new[]
        {
            ("TW",   0.50f, 0.88f),
            ("LV",   0.12f, 0.70f), ("IV_1", 0.37f, 0.70f), ("IV_2", 0.63f, 0.70f), ("RV", 0.88f, 0.70f),
            ("ZM_1", 0.22f, 0.46f), ("ZM_2", 0.50f, 0.46f), ("ZM_3", 0.78f, 0.46f),
            ("LA",   0.12f, 0.18f), ("ST",   0.50f, 0.18f), ("RA",   0.88f, 0.18f),
        },
    };

    // Field dimensions (fixed)
    private const float FieldW = 330f;
    private const float FieldH = 520f;
    private const float SlotW  = 64f;
    private const float SlotH  = 52f;

    private OptionButton _formationBtn = null!;
    private Control      _fieldControl = null!;
    private VBoxContainer _playerVBox  = null!;
    private Label         _statusLabel = null!;
    private Label         _staerkeLabel = null!;
    private Label         _warnungLabel = null!;
    private Button        _autoBtn = null!;

    private const int BankSpalten = 5;

    private Label         _bankLabel     = null!;
    private GridContainer _bankContainer = null!;
    private readonly List<PositionSlot> _bankSlots = new();
    private readonly Dictionary<long, PlayerRow> _playerRows = new();

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
        root.AddThemeConstantOverride("separation", 10);
        AddChild(root);

        root.AddChild(BuildHeader());

        _warnungLabel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        root.AddChild(_warnungLabel);

        var content = new HSplitContainer();
        content.SizeFlagsVertical = SizeFlags.ExpandFill;
        content.AddThemeConstantOverride("separation", 12);
        root.AddChild(content);

        content.AddChild(BuildField());
        content.AddChild(BuildPlayerList());
    }

    private Control BuildHeader()
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 12);

        hbox.AddChild(FmTheme.MakeLabel("🎯  Taktik – Aufstellung", 20, FmTheme.TextPrimary));

        var spacer1 = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        hbox.AddChild(spacer1);

        hbox.AddChild(FmTheme.MakeLabel("Formation:", 13, FmTheme.TextSecondary));

        _formationBtn = new OptionButton();
        _formationBtn.CustomMinimumSize = new Vector2(110, 0);
        foreach (var f in FormationNames)
            _formationBtn.AddItem(f);
        _formationBtn.AddThemeColorOverride("font_color", FmTheme.TextPrimary);
        _formationBtn.ItemSelected += OnFormationChanged;
        hbox.AddChild(_formationBtn);

        _autoBtn = new Button { Text = "⚡  Automatisch aufstellen" };
        FmTheme.ApplyButton(_autoBtn, FmTheme.BgPanel);
        _autoBtn.AddThemeColorOverride("font_color", FmTheme.TextPrimary);
        _autoBtn.TooltipText = "Besetzt jede Position mit dem stärksten verfügbaren Spieler "
                             + "und füllt die Ersatzbank.";
        _autoBtn.Pressed += async () => await AutomatischAufstellen();
        hbox.AddChild(_autoBtn);

        // Kein Speichern-Knopf: Jede Änderung wird sofort übernommen, gespielt wird immer
        // mit der aktuellen Aufstellung.

        _statusLabel = FmTheme.MakeLabel("", 12, FmTheme.TextSecondary);
        hbox.AddChild(_statusLabel);

        return hbox;
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
        if (warnung == null)
        {
            _warnungLabel.Text = "✓  Mannschaft vollständig aufgestellt";
            _warnungLabel.AddThemeColorOverride("font_color", FmTheme.Success);
            return;
        }

        // Zu wenige Spieler verhindern den Anpfiff, fehlende Positionen sind nur ein Hinweis.
        _warnungLabel.Text = (aufstellung.Spielbereit ? "⚠  " : "✖  ") + warnung;
        _warnungLabel.AddThemeColorOverride("font_color",
            aufstellung.Spielbereit ? FmTheme.Gold : FmTheme.Danger);
    }

    private Control BuildField()
    {
        var panel = new PanelContainer();
        panel.CustomMinimumSize = new Vector2(FieldW + 24, 0);
        panel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;

        var style = new StyleBoxFlat { BgColor = FmTheme.BgPanel };
        style.SetBorderWidthAll(1);
        style.SetContentMarginAll(0);
        panel.AddThemeStyleboxOverride("panel", style);

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 12);
        panel.AddChild(margin);

        var spalte = new VBoxContainer();
        spalte.AddThemeConstantOverride("separation", 10);
        margin.AddChild(spalte);

        _fieldControl = new Control();
        _fieldControl.CustomMinimumSize = new Vector2(FieldW, FieldH);
        spalte.AddChild(_fieldControl);

        var bankTrenner = new HSeparator();
        bankTrenner.AddThemeColorOverride("color", FmTheme.Border);
        spalte.AddChild(bankTrenner);

        _bankLabel = FmTheme.MakeLabel("Ersatzbank", 12, FmTheme.TextSecondary);
        spalte.AddChild(_bankLabel);

        // Die Anzahl der Plaetze kommt vom Server (pro Wettbewerb), daher erst beim Laden gefuellt.
        _bankContainer = new GridContainer { Columns = BankSpalten };
        _bankContainer.AddThemeConstantOverride("h_separation", 4);
        _bankContainer.AddThemeConstantOverride("v_separation", 4);
        spalte.AddChild(_bankContainer);

        DrawFieldBackground();
        BuildSlots(_currentFormation);

        _staerkeLabel = FmTheme.MakeLabel("Gesamtstärke: –", 12, FmTheme.TextPrimary);
        _staerkeLabel.Position = new Vector2(FieldW - 162, 6);
        _staerkeLabel.CustomMinimumSize = new Vector2(156, 0);
        _staerkeLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _staerkeLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        _staerkeLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _staerkeLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _staerkeLabel.TooltipText =
            "Durchschnitt der Stärke über alle Positionen der Formation.\n"
          + "Unbesetzte Positionen zählen dabei mit 0 - sonst wäre eine Elf mit neun\n"
          + "starken Spielern rechnerisch besser als eine vollständige mit denselben\n"
          + "neun plus zwei schwächeren.";
        _fieldControl.AddChild(_staerkeLabel);

        return panel;
    }

    private void DrawFieldBackground()
    {
        // Grüner Rasen
        var grass = new ColorRect
        {
            Color = new Color(0.13f, 0.40f, 0.18f),
            Size  = new Vector2(FieldW, FieldH),
        };
        _fieldControl.AddChild(grass);

        // Mittellinie
        AddFieldLine(0, FieldH * 0.5f, FieldW, FieldH * 0.5f, new Color(1, 1, 1, 0.25f));
        // Mittelkreis (approximiert durch schmales Rechteck)
        var circle = new ColorRect
        {
            Color              = new Color(1, 1, 1, 0.12f),
            CustomMinimumSize  = new Vector2(100, 100),
            Size               = new Vector2(100, 100),
        };
        circle.Position = new Vector2((FieldW - 100) * 0.5f, (FieldH - 100) * 0.5f);
        _fieldControl.AddChild(circle);

        // Strafraum oben (gegnerisch)
        AddFieldRect(FieldW * 0.18f, 0, FieldW * 0.64f, FieldH * 0.18f, new Color(1, 1, 1, 0.12f));
        // Strafraum unten (eigen)
        AddFieldRect(FieldW * 0.18f, FieldH * 0.82f, FieldW * 0.64f, FieldH * 0.18f, new Color(1, 1, 1, 0.12f));
    }

    private void AddFieldLine(float x1, float y1, float x2, float y2, Color color)
    {
        var line = new ColorRect
        {
            Color    = color,
            Position = new Vector2(x1, y1 - 1),
            Size     = new Vector2(x2 - x1, 2),
        };
        _fieldControl.AddChild(line);
    }

    private void AddFieldRect(float x, float y, float w, float h, Color color)
    {
        var outline = new ColorRect { Color = color, Position = new Vector2(x, y), Size = new Vector2(w, h) };
        _fieldControl.AddChild(outline);
    }

    private void BuildSlots(string formation)
    {
        foreach (var slot in _slots.Values)
            slot.QueueFree();
        _slots.Clear();

        var alleSlots = Formations[formation].Select(f => f.Item1).ToList();
        foreach (var (slotName, xRatio, yRatio) in Formations[formation])
        {
            var slot = PositionSlot.Create(slotName, SlotBezeichnung.Fuer(slotName, alleSlots));
            slot.Position = new Vector2(
                FieldW * xRatio - SlotW * 0.5f,
                FieldH * yRatio - SlotH * 0.5f);
            slot.Size = new Vector2(SlotW, SlotH);

            slot.PlayerDropped += OnPlayerDropped;
            slot.Pressed       += () => OnSlotPressed(slot);
            _slots[slotName] = slot;
            _fieldControl.AddChild(slot);
        }

        if (_staerkeLabel?.GetParent() == _fieldControl)
            _fieldControl.MoveChild(_staerkeLabel, _fieldControl.GetChildCount() - 1);
    }

    /// <summary>Legt so viele Bankplätze an, wie der Wettbewerb zulässt.</summary>
    private void BaueBank(int plaetze)
    {
        foreach (var slot in _bankSlots)
            slot.QueueFree();
        _bankSlots.Clear();

        foreach (Node child in _bankContainer.GetChildren())
            child.QueueFree();

        _bankLabel.Text = plaetze > 0
            ? $"Ersatzbank ({plaetze} Plätze)"
            : "Ersatzbank – in diesem Wettbewerb nicht vorgesehen";

        for (int i = 0; i < plaetze; i++)
        {
            var slot = PositionSlot.Create($"B{i + 1}", $"B{i + 1}");
            slot.CustomMinimumSize = new Vector2(SlotW, 44);
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
        var style = new StyleBoxFlat { BgColor = FmTheme.BgPanel };
        style.SetBorderWidthAll(1);
        style.SetContentMarginAll(0);
        panel.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 0);
        panel.AddChild(vbox);

        // Spalten-Header
        vbox.AddChild(BuildPlayerListHeader());

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        vbox.AddChild(sep);

        // Scroll-Bereich
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(scroll);

        _playerVBox = new VBoxContainer();
        _playerVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _playerVBox.AddThemeConstantOverride("separation", 1);
        scroll.AddChild(_playerVBox);

        return panel;
    }

    private static Control BuildPlayerListHeader()
    {
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 0);

        var headerStyle = new StyleBoxFlat { BgColor = FmTheme.BgToolbar };
        headerStyle.SetContentMarginAll(0);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", headerStyle);
        panel.AddChild(hbox);

        foreach (var (titel, breite, expand, ausrichtung) in PlayerRow.Spalten)
        {
            var m = new MarginContainer();
            m.AddThemeConstantOverride("margin_left", 6);
            m.AddThemeConstantOverride("margin_right", 6);
            m.AddThemeConstantOverride("margin_top", 4);
            m.AddThemeConstantOverride("margin_bottom", 4);
            m.CustomMinimumSize = new Vector2(breite, 0);
            if (expand) m.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            m.AddChild(FmTheme.MakeLabel(titel, 12, FmTheme.TextSecondary, ausrichtung));
            hbox.AddChild(m);
        }

        return panel;
    }

    // ── Daten laden ──────────────────────────────────────────────────────────

    private async System.Threading.Tasks.Task LadeAlles()
    {
        _statusLabel.Text = "Lade …";
        var vereinId = GameState.Instance.VereinId;

        var spielerTask     = ApiClient.GetAsync<List<Spieler>>($"verein/{vereinId}/spieler");
        var aufstellungTask = ApiClient.GetAsync<AufstellungModel>($"aufstellung/{vereinId}");

        _alleSpieler = await spielerTask ?? new List<Spieler>();

        foreach (var s in _alleSpieler)
            _playerNames[s.Id] = s.Name;

        FuelleSpielerListe(_alleSpieler);

        var aufstellung = await aufstellungTask;

        // Auch ohne gespeicherte Aufstellung liefert der Server die Bankgröße des Wettbewerbs.
        BaueBank(aufstellung?.MaxErsatzbank ?? 0);

        if (aufstellung?.Formation != null && Formations.ContainsKey(aufstellung.Formation))
        {
            _currentFormation = aufstellung.Formation;
            int idx = Array.IndexOf(FormationNames, _currentFormation);
            if (idx >= 0) _formationBtn.Selected = idx;
            BuildSlots(_currentFormation);
            WendeSpielerZuweisungenAn(aufstellung.Positionen, aufstellung.SlotStaerken,
                aufstellung.SlotGrundstaerken, aufstellung.SlotEingespieltheit);
            AktualisiereSaerkeLabel(aufstellung.Gesamtstaerke);
        }
        if (aufstellung != null)
        {
            WendeBankAn(aufstellung.Ersatzbank);
        }
        // Erst jetzt eintragen: Die Liste entsteht, bevor die Aufstellung geladen ist.
        AktualisiereListenPositionen();
        AktualisiereWarnung(aufstellung);

        _statusLabel.Text = $"{_alleSpieler.Count} Spieler";
    }

    private void FuelleSpielerListe(List<Spieler> spieler)
    {
        foreach (Node child in _playerVBox.GetChildren())
            child.QueueFree();

        // Nur Profi- und Amateurkader; Jugendspieler stehen hier nicht zur Wahl.
        // Sortiert nach Positionsgruppe (TW, Abwehr, Mittelfeld, Sturm), darin die stärkeren zuerst.
        var sichtbar = spieler
            .Where(s => s.Kader == "Profi" || s.Kader == "Amateur")
            .OrderBy(s => s.Sortierung.Item1)
            .ThenBy(s => s.Sortierung.Item2)
            .ThenBy(s => s.Name)
            .ToList();

        // Abwechselnde Helligkeit innerhalb einer Gruppe, damit Zeilen trennbar bleiben.
        Positionsgruppe? letzte = null;
        bool abgesetzt = false;
        _playerRows.Clear();
        foreach (var s in sichtbar)
        {
            if (letzte != s.Gruppe) { abgesetzt = false; letzte = s.Gruppe; }
            var zeile = PlayerRow.Create(s, abgesetzt);
            _playerRows[s.Id] = zeile;
            _playerVBox.AddChild(zeile);
            abgesetzt = !abgesetzt;
        }

        AktualisiereListenPositionen();
    }

    /// <summary>
    /// Traegt in die Liste ein, wer gerade wo steht. Wird nach jeder Aenderung aufgerufen,
    /// statt die Liste neu aufzubauen - so bleibt die Scrollposition erhalten.
    /// </summary>
    private void AktualisiereListenPositionen()
    {
        var belegung = new Dictionary<long, string>();

        foreach (var (_, slot) in _slots)
        {
            if (slot.SpielerId.HasValue)
                belegung[slot.SpielerId.Value] = slot.Anzeige;
        }
        foreach (var slot in _bankSlots)
        {
            if (slot.SpielerId.HasValue)
                belegung[slot.SpielerId.Value] = slot.Anzeige;
        }

        foreach (var (spielerId, zeile) in _playerRows)
            zeile.SetzeAufstellungsSlot(belegung.GetValueOrDefault(spielerId));
    }

    /// <summary>Lässt den Server die stärkste Elf und die Ersatzbank bestimmen.</summary>
    private async System.Threading.Tasks.Task AutomatischAufstellen()
    {
        _autoBtn.Disabled = true;
        _statusLabel.Text = "Stelle auf …";

        var vereinId = GameState.Instance.VereinId;
        var ergebnis = await ApiClient.PostAsync<object, AufstellungModel>(
            $"aufstellung/{vereinId}/automatisch?formation={_currentFormation}", new { });

        _autoBtn.Disabled = false;

        if (ergebnis == null)
        {
            _statusLabel.Text = "Automatische Aufstellung fehlgeschlagen";
            return;
        }

        WendeAufstellungAn(ergebnis);
        _statusLabel.Text = "Automatisch aufgestellt ✓";
    }

    /// <summary>Überträgt eine komplette Aufstellung vom Server auf die Oberfläche.</summary>
    private void WendeAufstellungAn(AufstellungModel aufstellung)
    {
        if (aufstellung.Formation != null && Formations.ContainsKey(aufstellung.Formation))
        {
            _currentFormation = aufstellung.Formation;
            int idx = Array.IndexOf(FormationNames, _currentFormation);
            if (idx >= 0) _formationBtn.Selected = idx;
        }

        // Neu aufbauen, damit alte Zuweisungen nicht stehen bleiben.
        BuildSlots(_currentFormation);
        foreach (var slot in _bankSlots)
            slot.Clear();

        WendeSpielerZuweisungenAn(aufstellung.Positionen, aufstellung.SlotStaerken,
            aufstellung.SlotGrundstaerken, aufstellung.SlotEingespieltheit);
        WendeBankAn(aufstellung.Ersatzbank);
        AktualisiereListenPositionen();
        AktualisiereWarnung(aufstellung);
        AktualisiereSaerkeLabel(aufstellung.Gesamtstaerke);
    }

    private void WendeBankAn(List<long>? ersatzbank)
    {
        if (ersatzbank == null) return;
        for (int i = 0; i < _bankSlots.Count && i < ersatzbank.Count; i++)
        {
            if (_playerNames.TryGetValue(ersatzbank[i], out var name))
            {
                _bankSlots[i].Assign(ersatzbank[i], name);
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

    private void OnFormationChanged(long index)
    {
        var newFormation = FormationNames[(int)index];
        if (newFormation == _currentFormation) return;

        // Bestehende Zuweisungen retten, sofern Slot noch existiert
        var alteZuweisungen = _slots
            .Where(kv => kv.Value.SpielerId.HasValue)
            .ToDictionary(kv => kv.Key, kv => (kv.Value.SpielerId!.Value, kv.Value.SpielerName, kv.Value.Staerke));

        _currentFormation = newFormation;
        BuildSlots(_currentFormation);

        foreach (var (slotName, (spielerId, name, staerke)) in alteZuweisungen)
        {
            if (_slots.TryGetValue(slotName, out var slot))
                slot.Assign(spielerId, name, staerke);
        }

        AktualisiereListenPositionen();
        // Auch der Formationswechsel muss uebernommen werden - vorher ging das nur
        // ueber den inzwischen entfallenen Speichern-Knopf.
        _ = UebernehmeAenderung();
        _statusLabel.Text = $"Formation: {_currentFormation}";
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

        // Wenn auf dem Ziel-Slot bereits jemand ist und der gedropte woanders herkam: tauschen
        if (targetSlot.SpielerId.HasValue && targetSlot.SpielerId != spielerId)
        {
            // kein Tausch zurück nötig – der vorherige wird einfach überschrieben
        }

        targetSlot.Assign(spielerId, spielerName);
        AktualisiereListenPositionen();
        _statusLabel.Text = $"{spielerName} → {targetSlot.Anzeige}";

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
            AktualisiereWarnung(result);
            AktualisiereSaerkeLabel(result.Gesamtstaerke);
            foreach (var (slotName, staerke) in result.SlotStaerken)
            {
                if (_slots.TryGetValue(slotName, out var slot) && slot.SpielerId.HasValue)
                {
                    result.SlotGrundstaerken.TryGetValue(slotName, out var grundstaerke);
                    result.SlotEingespieltheit.TryGetValue(slotName, out var eingespieltheit);
                    slot.UpdateStaerke(staerke, grundstaerke, eingespieltheit);
                }
            }
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
            ? $"Gesamtstärke: {gesamtstaerke.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("de-DE"))}"
            : "Gesamtstärke: –";
    }

}
