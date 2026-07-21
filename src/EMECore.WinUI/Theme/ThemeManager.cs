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
        Background = ColorFromHex("#0a0b0d"),
        Surface = ColorFromHex("#161719"),
        Card = ColorFromHex("#2a2d31"),
        CardHover = ColorFromHex("#3a3d43"),
        Accent = ColorFromHex("#4ccba0"),
        AccentSecondary = ColorFromHex("#35a982"),
        AccentGlow = ColorFromHex("#4ccba0"),
        Success = ColorFromHex("#4ccba0"),
        Warning = ColorFromHex("#e6a030"),
        Danger = ColorFromHex("#e03a44"),
        TextPrimary = ColorFromHex("#e8e9eb"),
        TextSecondary = ColorFromHex("#a8abb0"),
        TextMuted = ColorFromHex("#70747a"),
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

    public static readonly AppTheme BlueTheme = new()
    {
        Name = "Azul",
        Description = "Azul profundo com toques de ciano",
        Background = ColorFromHex("#060a10"),
        Surface = ColorFromHex("#0c1218"),
        Card = ColorFromHex("#111a24"),
        CardHover = ColorFromHex("#17222e"),
        Accent = ColorFromHex("#3b9eff"),
        AccentSecondary = ColorFromHex("#2070cc"),
        AccentGlow = ColorFromHex("#3b9eff"),
        Success = ColorFromHex("#00dd88"),
        Warning = ColorFromHex("#ffaa00"),
        Danger = ColorFromHex("#ff4455"),
        TextPrimary = ColorFromHex("#e0ecff"),
        TextSecondary = ColorFromHex("#6088b0"),
        TextMuted = ColorFromHex("#2a3a50"),
        Border = ColorFromHex("#17222e"),
        BorderHover = ColorFromHex("#3b9eff"),
    };

    public static readonly AppTheme PurpleTheme = new()
    {
        Name = "Roxo",
        Description = "Roxo vibrante com violeta",
        Background = ColorFromHex("#08060e"),
        Surface = ColorFromHex("#100c1a"),
        Card = ColorFromHex("#181224"),
        CardHover = ColorFromHex("#20182e"),
        Accent = ColorFromHex("#a855f7"),
        AccentSecondary = ColorFromHex("#7c3aed"),
        AccentGlow = ColorFromHex("#a855f7"),
        Success = ColorFromHex("#00dd88"),
        Warning = ColorFromHex("#f59e0b"),
        Danger = ColorFromHex("#ef4444"),
        TextPrimary = ColorFromHex("#f0e0ff"),
        TextSecondary = ColorFromHex("#8860b0"),
        TextMuted = ColorFromHex("#3a2a50"),
        Border = ColorFromHex("#20182e"),
        BorderHover = ColorFromHex("#a855f7"),
    };

    public static readonly AppTheme OrangeTheme = new()
    {
        Name = "Laranja",
        Description = "Laranja quente com âmbar",
        Background = ColorFromHex("#0c0804"),
        Surface = ColorFromHex("#160e08"),
        Card = ColorFromHex("#1f160e"),
        CardHover = ColorFromHex("#2a1e14"),
        Accent = ColorFromHex("#f97316"),
        AccentSecondary = ColorFromHex("#c2410c"),
        AccentGlow = ColorFromHex("#f97316"),
        Success = ColorFromHex("#00dd88"),
        Warning = ColorFromHex("#fbbf24"),
        Danger = ColorFromHex("#ef4444"),
        TextPrimary = ColorFromHex("#fff0e0"),
        TextSecondary = ColorFromHex("#b08060"),
        TextMuted = ColorFromHex("#4a3020"),
        Border = ColorFromHex("#2a1e14"),
        BorderHover = ColorFromHex("#f97316"),
    };

    public static readonly AppTheme MidnightTheme = new()
    {
        Name = "Meia-noite",
        Description = "Azul marinho profundo e elegante",
        Background = ColorFromHex("#05080e"),
        Surface = ColorFromHex("#0a1020"),
        Card = ColorFromHex("#0f1830"),
        CardHover = ColorFromHex("#14203a"),
        Accent = ColorFromHex("#60a5fa"),
        AccentSecondary = ColorFromHex("#3b82f6"),
        AccentGlow = ColorFromHex("#60a5fa"),
        Success = ColorFromHex("#34d399"),
        Warning = ColorFromHex("#fbbf24"),
        Danger = ColorFromHex("#f87171"),
        TextPrimary = ColorFromHex("#e0f0ff"),
        TextSecondary = ColorFromHex("#6090c0"),
        TextMuted = ColorFromHex("#203050"),
        Border = ColorFromHex("#14203a"),
        BorderHover = ColorFromHex("#60a5fa"),
    };

    public static AppTheme Current { get; private set; } = SteamTheme;
    public static AppTheme Previous { get; private set; } = SteamTheme;

    public static AppTheme[] AvailableThemes => new[] { SteamTheme, GamerTheme, NeonTheme, RedTheme, BlueTheme, PurpleTheme, OrangeTheme, MidnightTheme };

    public static void SetTheme(AppTheme theme)
    {
        Previous = Current;
        Current = theme;
        Design.C.RefreshColors(theme);
        AppStyles.Colors.RefreshColors(theme);
        SteamColors.RefreshColors(theme);
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    public static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

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
