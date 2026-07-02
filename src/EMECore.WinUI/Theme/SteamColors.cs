using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace EMECore.WinUI.Theme;

public static class SteamColors
{
    public static readonly Windows.UI.Color Dark = ColorFromHex("#1b2838");
    public static readonly Windows.UI.Color Darker = ColorFromHex("#171a21");
    public static readonly Windows.UI.Color Darkest = ColorFromHex("#0e1621");
    public static readonly Windows.UI.Color Card = ColorFromHex("#1e2a3a");
    public static readonly Windows.UI.Color CardHover = ColorFromHex("#2a475e");
    public static readonly Windows.UI.Color Hover = ColorFromHex("#3d6c8e");
    public static readonly Windows.UI.Color Blue = ColorFromHex("#66c0f4");
    public static readonly Windows.UI.Color Green = ColorFromHex("#a4d007");
    public static readonly Windows.UI.Color Orange = ColorFromHex("#cf6a32");
    public static readonly Windows.UI.Color Red = ColorFromHex("#d94126");
    public static readonly Windows.UI.Color Text = ColorFromHex("#c7d5e0");
    public static readonly Windows.UI.Color TextSecondary = ColorFromHex("#8b9bb4");
    public static readonly Windows.UI.Color Light = Colors.White;

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

    private static Windows.UI.Color ColorFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        return Windows.UI.Color.FromArgb(255,
            byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber));
    }

    public static void ApplyToApplication(Microsoft.UI.Xaml.Application app)
    {
        var resources = app.Resources;

        resources["SteamDarkColor"] = Dark;
        resources["SteamDarkerColor"] = Darker;
        resources["SteamDarkestColor"] = Darkest;
        resources["SteamCardColor"] = Card;
        resources["SteamCardHoverColor"] = CardHover;
        resources["SteamHoverColor"] = Hover;
        resources["SteamBlueColor"] = Blue;
        resources["SteamGreenColor"] = Green;
        resources["SteamOrangeColor"] = Orange;
        resources["SteamRedColor"] = Red;
        resources["SteamTextColor"] = Text;
        resources["SteamTextSecondaryColor"] = TextSecondary;
        resources["SteamLightColor"] = Light;

        resources["SteamDarkBrush"] = DarkBrush;
        resources["SteamDarkerBrush"] = DarkerBrush;
        resources["SteamDarkestBrush"] = DarkestBrush;
        resources["SteamCardBrush"] = CardBrush;
        resources["SteamCardHoverBrush"] = CardHoverBrush;
        resources["SteamHoverBrush"] = HoverBrush;
        resources["SteamBlueBrush"] = BlueBrush;
        resources["SteamGreenBrush"] = GreenBrush;
        resources["SteamOrangeBrush"] = OrangeBrush;
        resources["SteamRedBrush"] = RedBrush;
        resources["SteamTextBrush"] = TextBrush;
        resources["SteamTextSecondaryBrush"] = TextSecondaryBrush;
        resources["SteamLightBrush"] = LightBrush;
    }
}
