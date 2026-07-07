using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace EMECore.WinUI.Theme;

public static class SteamColors
{
    public static readonly Color Dark = AppStyles.Colors.Bg;
    public static readonly Color Darker = AppStyles.Colors.Side;
    public static readonly Color Darkest = AppStyles.Colors.Inset;
    public static readonly Color Card = AppStyles.Colors.Card;
    public static readonly Color CardHover = AppStyles.Colors.Sec;
    public static readonly Color Hover = AppStyles.Colors.Primary;
    public static readonly Color Blue = AppStyles.Colors.Primary;
    public static readonly Color Green = AppStyles.Colors.Primary;
    public static readonly Color Orange = AppStyles.Colors.Warn;
    public static readonly Color Red = AppStyles.Colors.Danger;
    public static readonly Color Text = AppStyles.Colors.Fg;
    public static readonly Color TextSecondary = AppStyles.Colors.Muted;
    public static readonly Color Light = Microsoft.UI.Colors.White;

    public static readonly SolidColorBrush DarkBrush = new(Dark);
    public static readonly SolidColorBrush DarkerBrush = new(Darker);
    public static readonly SolidColorBrush DarkestBrush = new(Darkest);
    public static readonly SolidColorBrush CardBrush = new(Card);
    public static readonly SolidColorBrush CardHoverBrush = new(CardHover);
    public static readonly SolidColorBrush HoverBrush = new(Hover);
    public static readonly SolidColorBrush BlueBrush = new(Blue);
    public static readonly SolidColorBrush GreenBrush = new(Green);
    public static readonly SolidColorBrush OrangeBrush = new(Orange);
    public static readonly SolidColorBrush RedBrush = new(Red);
    public static readonly SolidColorBrush TextBrush = new(Text);
    public static readonly SolidColorBrush TextSecondaryBrush = new(TextSecondary);
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
