using Godot;

namespace FMDesktop.UI;

public partial class GameMain : Control
{
    private Control _contentArea = null!;
    private Label   _vereinLabel = null!;

    private const string SceneKader       = "res://scenes/mannschaft/KaderView.tscn";
    private const string SceneJugend      = "res://scenes/jugend/JugendView.tscn";
    private const string SceneTabelle     = "res://scenes/liga/TabelleView.tscn";
    private const string SceneSpielplan   = "res://scenes/liga/SpielplanView.tscn";
    private const string SceneStatistiken = "res://scenes/liga/StatistikenView.tscn";

    public override void _Ready()
    {
        BuildUI();
        LadeScene(SceneKader);
    }

    private void BuildUI()
    {
        // Hintergrund
        var bg = new ColorRect { Color = FmTheme.BgDark };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 0);
        AddChild(vbox);

        // Toolbar
        vbox.AddChild(BuildToolbar());

        // Trennlinie
        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", FmTheme.Border);
        vbox.AddChild(sep);

        // Inhaltsbereich
        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 16);
        margin.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(margin);

        _contentArea = new Control();
        _contentArea.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddChild(_contentArea);
    }

    private Control BuildToolbar()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", FmTheme.ToolbarStyle());

        var margin = new MarginContainer();
        FmTheme.SetMargin(margin, 8, 6);
        panel.AddChild(margin);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 4);
        margin.AddChild(hbox);

        // Zurück-Button
        var back = new Button { Text = "←", TooltipText = "Hauptmenü", CustomMinimumSize = new Vector2(36, 0) };
        FmTheme.ApplyButton(back, FmTheme.BgPanel);
        back.AddThemeColorOverride("font_color", FmTheme.TextSecondary);
        back.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/StartScreen.tscn");
        hbox.AddChild(back);

        hbox.AddChild(new VSeparator());

        // Mannschaft-Dropdown
        hbox.AddChild(BuildDropdown("👥  Mannschaft", new[] {
            ("Kader",       SceneKader),
            ("Aufstellung", ""),          // noch nicht implementiert
        }));

        // Liga-Dropdown
        hbox.AddChild(BuildDropdown("🏆  Liga", new[] {
            ("Tabelle",     SceneTabelle),
            ("Spielplan",   SceneSpielplan),
            ("Statistiken", SceneStatistiken),
        }));

        // Einfache Menüpunkte
        foreach (var (label, scene) in new (string, string)[] {
            ("💪  Training", ""),
            ("💶  Finanzen", ""),
            ("🏟  Umfeld",   ""),
            ("👦  Jugend",   SceneJugend),
        })
        {
            var btn = new Button { Text = label };
            FmTheme.ApplyButton(btn, FmTheme.BgPanel);
            btn.AddThemeColorOverride("font_color", FmTheme.TextPrimary);
            var s = scene;
            btn.Pressed += () => { if (!string.IsNullOrEmpty(s)) LadeScene(s); };
            hbox.AddChild(btn);
        }

        // Spacer
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        hbox.AddChild(spacer);

        // Vereinsname rechts
        _vereinLabel = FmTheme.MakeLabel(GameState.Instance.VereinName, 14, FmTheme.TextPrimary);
        hbox.AddChild(_vereinLabel);

        return panel;
    }

    private MenuButton BuildDropdown(string label, (string Label, string Scene)[] items)
    {
        var btn = new MenuButton { Text = label };
        btn.AddThemeStyleboxOverride("normal", FmTheme.ButtonStyle(FmTheme.BgPanel));
        btn.AddThemeColorOverride("font_color", FmTheme.TextPrimary);

        var popup = btn.GetPopup();
        for (int i = 0; i < items.Length; i++)
            popup.AddItem(items[i].Label, i);

        var captured = items;
        popup.IdPressed += id =>
        {
            var scene = captured[(int)id].Scene;
            if (!string.IsNullOrEmpty(scene)) LadeScene(scene);
        };

        return btn;
    }

    private void LadeScene(string path)
    {
        foreach (Node child in _contentArea.GetChildren())
            child.QueueFree();

        var packed = GD.Load<PackedScene>(path);
        if (packed == null)
        {
            GD.PrintErr($"[GameMain] Szene nicht gefunden: {path}");
            return;
        }
        var instance = packed.Instantiate<Control>();
        instance.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _contentArea.AddChild(instance);
    }
}
