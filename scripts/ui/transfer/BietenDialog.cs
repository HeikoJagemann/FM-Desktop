#nullable enable
using System;
using Godot;
using FMDesktop.Api;
using FMDesktop.Models;

namespace FMDesktop.UI.Transfer;

/// <summary>
/// Ein Angebot für einen fremden Spieler - mit selbst gewählter Ablöse.
///
/// <para>Für einen Spieler auf der Transferliste ist der Preis vorgegeben. Wer abwerben will,
/// kennt die Forderung nicht: Er bietet, und der Verein nennt sie in seiner Absage. Geantwortet
/// wird beim nächsten Schritt im Kalender.</para>
/// </summary>
public partial class BietenDialog : Overlaykarte
{
    private TransferSpieler _spieler = null!;
    private Action<string>? _abgegeben;
    private SpinBox _betrag = null!;
    private Label _betragText = null!;

    public BietenDialog() : base("Angebot abgeben") { }

    /// <param name="abgegeben">bekommt die Erfolgsmeldung, wenn das Angebot raus ist</param>
    public static void Zeige(Node caller, TransferSpieler spieler, Action<string> abgegeben)
    {
        Oeffne(caller, new BietenDialog { _spieler = spieler, _abgegeben = abgegeben });
    }

    protected override void BaueInhalt(VBoxContainer inhalt)
    {
        var s = _spieler;
        inhalt.AddChild(FmTheme.MakeLabel($"{s.Name}  ·  {s.Position}, {s.Alter} Jahre", 16, FmTheme.TextPrimary));
        inhalt.AddChild(FmTheme.MakeLabel($"{s.Verein} ({s.Liga})", 13, FmTheme.TextSecondary));

        inhalt.AddChild(Zeile("Stärke", FmTheme.MakeLabel(s.StaerkeText, 13, FmTheme.TextPrimary)));
        inhalt.AddChild(Zeile("Talent", FmTheme.MakeLabel(s.TalentText, 13, FmTheme.TextPrimary)));
        inhalt.AddChild(Zeile("Marktwert", FmTheme.MakeLabel(FmTheme.Geld(s.Wert), 13, FmTheme.TextPrimary)));
        inhalt.AddChild(Zeile("Vertrag", FmTheme.MakeLabel(s.VertragText, 13, FmTheme.TextPrimary)));
        inhalt.AddChild(Zeile(FmTheme.GehaltTitel,
            FmTheme.MakeLabel(FmTheme.Gehalt(s.Gehaltsforderung), 13, FmTheme.TextPrimary)));

        var hinweis = FmTheme.MakeLabel(s.Preis > 0
                ? $"Steht auf der Transferliste - der Verein verlangt {FmTheme.Geld(s.Preis)}."
                : "Steht nicht zum Verkauf. Bietest du zu wenig, nennt der Verein in seiner Absage, was er verlangt.",
            13, FmTheme.TextSecondary);
        hinweis.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hinweis.CustomMinimumSize = new Vector2(400, 0);
        inhalt.AddChild(hinweis);

        var betragZeile = new HBoxContainer();
        betragZeile.AddThemeConstantOverride("separation", 10);
        _betrag = new SpinBox
        {
            MinValue = 0, MaxValue = 500_000_000, Step = 1_000, Value = s.Vorschlag,
            CustomMinimumSize = new Vector2(160, 0), Suffix = "€",
        };
        _betragText = FmTheme.MakeLabel(FmTheme.Geld(s.Vorschlag), 15, FmTheme.Accent);
        _betrag.ValueChanged += wert => _betragText.Text = FmTheme.Geld((long)wert);
        betragZeile.AddChild(_betrag);
        betragZeile.AddChild(_betragText);
        inhalt.AddChild(Zeile("Ablöse", betragZeile));

        inhalt.AddChild(FmTheme.MakeLabel("Der Verein antwortet beim nächsten Schritt im Kalender.",
            12, FmTheme.TextGedimmt));
        inhalt.AddChild(Knopfleiste("Angebot abgeben", async () => await Biete()));
    }

    private async System.Threading.Tasks.Task Biete()
    {
        long betrag = (long)_betrag.Value;
        long vereinId = GameState.Instance.VereinId;
        var antwort = await ApiClient.SendeAsync("POST",
            $"transfer/angebot?spielerId={_spieler.SpielerId}&vereinId={vereinId}&abloese={betrag}");
        if (!IsInstanceValid(this)) return;

        if (!antwort.Ok)
        {
            ZeigeFehler(antwort.Fehler ?? "Das Angebot wurde abgelehnt.");
            return;
        }
        _abgegeben?.Invoke($"Angebot für {_spieler.Name} über {FmTheme.Geld(betrag)} abgegeben - "
                           + "die Antwort kommt beim nächsten Schritt im Kalender.");
        Schliessen();
    }
}
