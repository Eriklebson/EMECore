using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace EMECore.WinUI.Theme;

public static class SteamColors
{
    public static Color Dark => DarkBrush.Color;
    public static Color Darker => DarkerBrush.Color;
    public static Color Darkest => DarkestBrush.Color;
    public static Color Card => CardBrush.Color;
    public static Color CardHover => CardHoverBrush.Color;
    public static Color Hover => HoverBrush.Color;
    public static Color Blue => BlueBrush.Color;
    public static Color Green => GreenBrush.Color;
    public static Color Orange => OrangeBrush.Color;
    public static Color Red => RedBrush.Color;
    public static Color Text => TextBrush.Color;
    public static Color TextSecondary => TextSecondaryBrush.Color;
    public static readonly Color Light = Microsoft.UI.Colors.White;

    public static readonly SolidColorBrush DarkBrush = new(AppStyles.Colors.Bg);
    public static readonly SolidColorBrush DarkerBrush = new(AppStyles.Colors.Side);
    public static readonly SolidColorBrush DarkestBrush = new(AppStyles.Colors.Inset);
    public static readonly SolidColorBrush CardBrush = new(AppStyles.Colors.Card);
    public static readonly SolidColorBrush CardHoverBrush = new(AppStyles.Colors.Sec);
    public static readonly SolidColorBrush HoverBrush = new(AppStyles.Colors.Primary);
    public static readonly SolidColorBrush BlueBrush = new(AppStyles.Colors.Primary);
    public static readonly SolidColorBrush GreenBrush = new(AppStyles.Colors.Primary);
    public static readonly SolidColorBrush OrangeBrush = new(AppStyles.Colors.Warn);
    public static readonly SolidColorBrush RedBrush = new(AppStyles.Colors.Danger);
    public static readonly SolidColorBrush TextBrush = new(AppStyles.Colors.Fg);
    public static readonly SolidColorBrush TextSecondaryBrush = new(AppStyles.Colors.Muted);
    public static readonly SolidColorBrush LightBrush = new(Light);

    public static void RefreshColors(AppTheme theme)
    {
        DarkBrush.Color = theme.Background; DarkerBrush.Color = theme.Surface; DarkestBrush.Color = theme.Background;
        CardBrush.Color = theme.Card; CardHoverBrush.Color = theme.CardHover; HoverBrush.Color = theme.Accent;
        BlueBrush.Color = theme.Accent; GreenBrush.Color = theme.Success; OrangeBrush.Color = theme.Warning;
        RedBrush.Color = theme.Danger; TextBrush.Color = theme.TextPrimary; TextSecondaryBrush.Color = theme.TextSecondary;
    }

    public static void ApplyToApplication(Microsoft.UI.Xaml.Application app)
    {
        var r = app.Resources;
        r["SteamDarkBrush"] = DarkBrush; r["SteamBlueBrush"] = BlueBrush;
        r["SteamTextBrush"] = TextBrush;
    }
}
