using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace EMECore.WinUI.Theme;

public class AppTheme
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    // Background layers (darkest to lightest)
    public Color Background { get; set; }
    public Color Surface { get; set; }
    public Color Card { get; set; }
    public Color CardHover { get; set; }

    // Accent
    public Color Accent { get; set; }
    public Color AccentSecondary { get; set; }
    public Color AccentGlow { get; set; }

    // Status
    public Color Success { get; set; }
    public Color Warning { get; set; }
    public Color Danger { get; set; }

    // Text
    public Color TextPrimary { get; set; }
    public Color TextSecondary { get; set; }
    public Color TextMuted { get; set; }

    // Borders
    public Color Border { get; set; }
    public Color BorderHover { get; set; }

    // Brushes (auto-generated from colors)
    public SolidColorBrush BackgroundBrush => new(Background);
    public SolidColorBrush SurfaceBrush => new(Surface);
    public SolidColorBrush CardBrush => new(Card);
    public SolidColorBrush CardHoverBrush => new(CardHover);
    public SolidColorBrush AccentBrush => new(Accent);
    public SolidColorBrush AccentSecondaryBrush => new(AccentSecondary);
    public SolidColorBrush AccentGlowBrush => new(AccentGlow);
    public SolidColorBrush SuccessBrush => new(Success);
    public SolidColorBrush WarningBrush => new(Warning);
    public SolidColorBrush DangerBrush => new(Danger);
    public SolidColorBrush TextPrimaryBrush => new(TextPrimary);
    public SolidColorBrush TextSecondaryBrush => new(TextSecondary);
    public SolidColorBrush TextMutedBrush => new(TextMuted);
    public SolidColorBrush BorderBrush => new(Border);
    public SolidColorBrush BorderHoverBrush => new(BorderHover);
}

public static class ThemeManager
{
    public static event EventHandler? ThemeChanged;

    public static readonly AppTheme SteamTheme = new()
    {
        Name = "Padrão",
        Description = "Tema escuro com verde-água — layout oficial E.M.E Core",
        Background = ColorFromHex("#1a1a1d"),
        Surface = ColorFromHex("#2a2a2e"),
        Card = ColorFromHex("#2a2a2e"),
        CardHover = ColorFromHex("#35353a"),
        Accent = ColorFromHex("#3dcc91"),
        AccentSecondary = ColorFromHex("#2ea87a"),
        AccentGlow = ColorFromHex("#3dcc91"),
        Success = ColorFromHex("#3dcc91"),
        Warning = ColorFromHex("#e6a030"),
        Danger = ColorFromHex("#e03a44"),
        TextPrimary = ColorFromHex("#e4e4e4"),
        TextSecondary = ColorFromHex("#a8aab0"),
        TextMuted = ColorFromHex("#707075"),
        Border = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF),
        BorderHover = Color.FromArgb(0x19, 0xFF, 0xFF, 0xFF),
    };

    public static readonly AppTheme GamerTheme = new()
    {
        Name = "Cyberpunk",
        Description = "Visual neon gamer com efeitos de brilho",
        Background = ColorFromHex("#0a0a0f"),
        Surface = ColorFromHex("#0f0f1a"),
        Card = ColorFromHex("#141428"),
        CardHover = ColorFromHex("#1a1a35"),
        Accent = ColorFromHex("#00f0ff"),
        AccentSecondary = ColorFromHex("#b44aff"),
        AccentGlow = ColorFromHex("#00f0ff"),
        Success = ColorFromHex("#00ff88"),
        Warning = ColorFromHex("#ffaa00"),
        Danger = ColorFromHex("#ff2255"),
        TextPrimary = ColorFromHex("#e0e0ff"),
        TextSecondary = ColorFromHex("#7878a0"),
        TextMuted = ColorFromHex("#3a3a55"),
        Border = ColorFromHex("#1a1a35"),
        BorderHover = ColorFromHex("#00f0ff"),
    };

    public static readonly AppTheme NeonTheme = new()
    {
        Name = "Neon",
        Description = "Verde neon sobre fundo escuro",
        Background = ColorFromHex("#050a08"),
        Surface = ColorFromHex("#0a1510"),
        Card = ColorFromHex("#0f1f18"),
        CardHover = ColorFromHex("#152a20"),
        Accent = ColorFromHex("#00ff66"),
        AccentSecondary = ColorFromHex("#00cc52"),
        AccentGlow = ColorFromHex("#00ff66"),
        Success = ColorFromHex("#00ff66"),
        Warning = ColorFromHex("#ccff00"),
        Danger = ColorFromHex("#ff3344"),
        TextPrimary = ColorFromHex("#d0ffe0"),
        TextSecondary = ColorFromHex("#60a080"),
        TextMuted = ColorFromHex("#2a4a35"),
        Border = ColorFromHex("#152a20"),
        BorderHover = ColorFromHex("#00ff66"),
    };

    public static readonly AppTheme RedTheme = new()
    {
        Name = "Vermelho",
        Description = "Vermelho intenso com fundo escuro",
        Background = ColorFromHex("#0a0508"),
        Surface = ColorFromHex("#150a10"),
        Card = ColorFromHex("#1f0f18"),
        CardHover = ColorFromHex("#2a1520"),
        Accent = ColorFromHex("#ff2255"),
        AccentSecondary = ColorFromHex("#cc1144"),
        AccentGlow = ColorFromHex("#ff2255"),
        Success = ColorFromHex("#00ff88"),
        Warning = ColorFromHex("#ffaa00"),
        Danger = ColorFromHex("#ff2255"),
        TextPrimary = ColorFromHex("#ffe0e8"),
        TextSecondary = ColorFromHex("#a06080"),
        TextMuted = ColorFromHex("#4a2a35"),
        Border = ColorFromHex("#2a1520"),
        BorderHover = ColorFromHex("#ff2255"),
    };

    public static AppTheme Current { get; private set; } = SteamTheme;

    public static AppTheme[] AvailableThemes => new[] { SteamTheme, GamerTheme, NeonTheme, RedTheme };

    public static void SetTheme(AppTheme theme)
    {
        Current = theme;
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static Color ColorFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            var r = byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber);
            var g = byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber);
            var b = byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber);
            return Color.FromArgb(255, r, g, b);
        }
        return Colors.White;
    }
}
