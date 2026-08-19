#nullable enable
using Godot;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI;

/// <summary>Modales Overlay mit Ergebnis und Minutenverlauf eines Spiels.</summary>
public partial class SpielberichtOverlay : Control
{
    private static SpielberichtOverlay? _aktuelleInstanz;

    private long _spielId;
    private VBoxContainer _inhalt = null!;
    private Label _titel = null!;
    private Label _untertitel = null!;

    public static void Zeige(Node caller, long spielId)
    {
        if (_aktuelleInstanz != null && IsInstanceValid(_aktuelleInstanz))
        {
            if (_aktuelleInstanz._spielId == spielId) return;
            _aktuelleInstanz.QueueFree();
        }

        var overlay = new SpielberichtOverlay { _spielId = spielId };
        _aktuelleInstanz = overlay;
        var scene = caller.GetTree().CurrentScene ?? caller.GetTree().Root;
        scene.AddChild(overlay);
    }

    public override void _ExitTree()
    {
        if (_aktuelleInstanz == this) _aktuelleInstanz = null;
    }

    public override async void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        bg.GuiInput += OnHintergrundInput;
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(center);

        center.AddChild(BuildCard());

        await LadeBericht();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            QueueFree();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnHintergrundInput(InputEvent ev)
    {
        if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            QueueFree();
    }

    private Control BuildCard()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(560, 0) };
        panel.AddThemeStyleboxOverride("panel", FmTheme.PanelStyle(10));

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 22);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        margin.AddChild(root);

        // Kopfzeile
        var kopf = new HBoxContainer();
        kopf.AddThemeConstantOverride("separation", 12);

        var titelBox = new VBoxContainer();
        titelBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _titel = FmTheme.MakeLabel("Lade …", 20, FmTheme.TextPrimary);
        _untertitel = FmTheme.MakeLabel("", 13, FmTheme.TextSecondary);
        titelBox.AddChild(_titel);
        titelBox.AddChild(_untertitel);
        kopf.AddChild(titelBox);

        var close = new Button { Text = "✕", CustomMinimumSize = new Vector2(32, 32) };
        FmTheme.ApplyButton(close, FmTheme.BgPanel);
        close.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
        close.Pressed += QueueFree;
        kopf.AddChild(close);

        root.AddChild(kopf);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        root.AddChild(sep);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 340),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddChild(scroll);

        _inhalt = new VBoxContainer();
        _inhalt.AddThemeConstantOverride("separation", 6);
        _inhalt.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_inhalt);

        return panel;
    }

    private async System.Threading.Tasks.Task LadeBericht()
    {
        var bericht = await ApiClient.GetAsync<SpielBericht>($"spiel/{_spielId}/bericht");
        if (bericht == null)
        {
            _titel.Text = "Spielbericht nicht verfügbar";
            return;
        }

        _titel.Text = $"{bericht.HeimVerein}  {bericht.Ergebnis}  {bericht.GastVerein}";
        _untertitel.Text = $"Saison {bericht.Saison} · Spieltag {bericht.Spieltag}";

        if (!bericht.Gespielt)
        {
            _inhalt.AddChild(FmTheme.MakeLabel("Dieses Spiel wurde noch nicht ausgetragen.",
                13, FmTheme.TextSecondary));
            return;
        }

        if (bericht.Ereignisse.Count == 0)
        {
            _inhalt.AddChild(FmTheme.MakeLabel("Ein ereignisloses Spiel – keine Tore, keine Karten.",
                13, FmTheme.TextSecondary));
            return;
        }

        foreach (var ereignis in bericht.Ereignisse)
            _inhalt.AddChild(BaueZeile(bericht, ereignis));
    }

    private static Control BaueZeile(SpielBericht bericht, SpielEreignis ereignis)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 10);

        var minute = FmTheme.MakeLabel($"{ereignis.Minute}'", 13, FmTheme.TextSecondary,
            HorizontalAlignment.Right);
        minute.CustomMinimumSize = new Vector2(38, 0);
        zeile.AddChild(minute);

        var symbol = FmTheme.MakeLabel(ereignis.Symbol, 13, FmTheme.TextPrimary);
        symbol.CustomMinimumSize = new Vector2(30, 0);
        zeile.AddChild(symbol);

        // Tore der eigenen Mannschaft hervorheben, damit der Verlauf schnell lesbar ist.
        bool eigenes = ereignis.VereinId == GameState.Instance.VereinId;
        var farbe = ereignis.IstTor
            ? (eigenes ? FmTheme.Accent : FmTheme.TextPrimary)
            : FmTheme.TextSecondary;

        var text = FmTheme.MakeLabel(ereignis.Beschreibung ?? "", 13, farbe);
        text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        zeile.AddChild(text);

        // Zwischenstand nur bei Toren
        if (ereignis.IstTor)
        {
            var verein = ereignis.VereinId == bericht.HeimVereinId
                ? bericht.HeimVerein
                : bericht.GastVerein;
            var seite = FmTheme.MakeLabel(verein ?? "", 12, FmTheme.TextSecondary,
                HorizontalAlignment.Right);
            seite.CustomMinimumSize = new Vector2(150, 0);
            zeile.AddChild(seite);
        }

        return zeile;
    }
}
