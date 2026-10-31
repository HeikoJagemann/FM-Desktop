#nullable enable
using Godot;

namespace FMDesktop.UI;

/// <summary>
/// Der Hauptknopf einer Ansicht: Bernstein, mit Symbol, Titel und einer Unterzeile, die sagt,
/// wohin es geht - "Weiter · bis Wochenstart, Mo." statt nur "Weiter".
/// </summary>
public partial class AktionsKnopf : Button
{
    private Label _titel = null!;
    private Label _unterzeile = null!;
    private Symbol _symbol = null!;
    private string _titelText = "";
    private string _unterzeileText = "";
    private bool _ohneUnterzeile;

    /// <summary>Blendet die Unterzeile aus, wo der Platz knapp ist - sie gehört dann in den Tooltip.</summary>
    public bool OhneUnterzeile
    {
        get => _ohneUnterzeile;
        set
        {
            if (_ohneUnterzeile == value) return;
            _ohneUnterzeile = value;
            Setze(_titelText, _unterzeileText);
        }
    }

    public static AktionsKnopf Neu(Symbol.Art symbol, string titel, string unterzeile = "")
    {
        var k = new AktionsKnopf { CustomMinimumSize = new Vector2(0, 48) };
        FmTheme.ApplyPrimaryButton(k);
        foreach (var zustand in new[] { "normal", "hover", "pressed", "disabled" })
        {
            var stil = (StyleBoxFlat)k.GetThemeStylebox(zustand).Duplicate();
            stil.ContentMarginLeft = 16;
            stil.ContentMarginRight = 20;
            k.AddThemeStyleboxOverride(zustand, stil);
        }

        var zeile = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        zeile.AddThemeConstantOverride("separation", 12);
        zeile.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        zeile.OffsetLeft = 16;
        zeile.OffsetRight = -20;
        k.AddChild(zeile);

        k._symbol = Symbol.Neu(symbol, FmTheme.AccentText, 20);
        zeile.AddChild(k._symbol);

        var texte = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        texte.AddThemeConstantOverride("separation", -2);
        zeile.AddChild(texte);

        k._titel = FmTheme.MakeLabel(titel, 15, FmTheme.AccentText);
        k._titel.AddThemeFontOverride("font", FmTheme.SchriftUi(700));
        k._titel.MouseFilter = MouseFilterEnum.Ignore;
        texte.AddChild(k._titel);

        k._unterzeile = FmTheme.MakeLabel(unterzeile, 11, new Color(FmTheme.AccentText, 0.8f));
        k._unterzeile.AddThemeFontOverride("font", FmTheme.SchriftUi(600));
        k._unterzeile.MouseFilter = MouseFilterEnum.Ignore;
        k._unterzeile.Visible = unterzeile != "";
        texte.AddChild(k._unterzeile);

        k.Setze(titel, unterzeile);
        return k;
    }

    public void Setze(string titel, string unterzeile = "")
    {
        _titelText = titel;
        _unterzeileText = unterzeile;
        if (_ohneUnterzeile) unterzeile = "";
        _titel.Text = titel;
        _unterzeile.Text = unterzeile;
        _unterzeile.Visible = unterzeile != "";
        // Der Knopf trägt seinen Text in Kindern; die Mindestbreite muss er daraus selbst kennen.
        var schriftTitel = FmTheme.SchriftUi(700);
        var schriftUnter = FmTheme.SchriftUi(600);
        float breite = Mathf.Max(
            schriftTitel.GetStringSize(titel, HorizontalAlignment.Left, -1, 15).X,
            unterzeile == "" ? 0 : schriftUnter.GetStringSize(unterzeile, HorizontalAlignment.Left, -1, 11).X);
        CustomMinimumSize = new Vector2(16 + 20 + 12 + breite + 20, 48);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDraw)
        {
            float deckkraft = Disabled ? 0.55f : 1f;
            _symbol.Modulate = new Color(1, 1, 1, deckkraft);
            _titel.Modulate = new Color(1, 1, 1, deckkraft);
            _unterzeile.Modulate = new Color(1, 1, 1, deckkraft);
        }
    }
}
