using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace EMECore.WinUI.Theme;

public static class AppStyles
{
    public static class Colors
    {
        public static readonly Color Bg      = Color.FromArgb(255, 0x0A, 0x0B, 0x0D);
        public static readonly Color Side    = Color.FromArgb(255, 0x16, 0x17, 0x19);
        public static readonly Color Card    = Color.FromArgb(255, 0x2A, 0x2D, 0x31);
        public static readonly Color Inset   = Color.FromArgb(255, 0x1B, 0x1D, 0x22);
        public static readonly Color Fg      = Color.FromArgb(255, 0xE8, 0xE9, 0xEB);
        public static readonly Color Muted   = Color.FromArgb(255, 0xA8, 0xAB, 0xB0);
        public static readonly Color Muted70 = Color.FromArgb(0xB3, 0xA8, 0xAB, 0xB0);
        public static readonly Color Primary = Color.FromArgb(255, 0x4C, 0xCB, 0xA0);
        public static readonly Color Warn    = Color.FromArgb(255, 0xE6, 0xA0, 0x30);
        public static readonly Color Danger  = Color.FromArgb(255, 0xE8, 0x4D, 0x4D);
        public static readonly Color Sec     = Color.FromArgb(255, 0x3A, 0x3D, 0x43);
        public static readonly Color Border  = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);

        public static readonly SolidColorBrush BgBrush    = new(Bg);
        public static readonly SolidColorBrush SideBrush  = new(Side);
        public static readonly SolidColorBrush CardBrush  = new(Card);
        public static readonly SolidColorBrush InsetBrush = new(Inset);
        public static readonly SolidColorBrush FgBrush    = new(Fg);
        public static readonly SolidColorBrush MutedBrush = new(Muted);
        public static readonly SolidColorBrush Muted70B   = new(Muted70);
        public static readonly SolidColorBrush PriBrush   = new(Primary);
        public static readonly SolidColorBrush Pri10      = new(Color.FromArgb(0x1A, 0x4C, 0xCB, 0xA0));
        public static readonly SolidColorBrush Pri5       = new(Color.FromArgb(0x0D, 0x4C, 0xCB, 0xA0));
        public static readonly SolidColorBrush DangerBrush = new(Danger);
        public static readonly SolidColorBrush SecBrush   = new(Sec);
        public static readonly SolidColorBrush BorBrush   = new(Border);
    }

    public static class Rad { public static readonly CornerRadius Xl = new(12); public static readonly CornerRadius Lg = new(8); public static readonly CornerRadius Md = new(6); }
    public static class Radius { public static readonly CornerRadius Xxl = new(16); public static readonly CornerRadius Xl = new(12); public static readonly CornerRadius Lg = new(8); public static readonly CornerRadius Md = new(6); }

    public static Border CardWrap(UIElement c) => new() { Background = Colors.CardBrush, CornerRadius = Rad.Xl, Padding = new Thickness(20), BorderThickness = new Thickness(1), BorderBrush = Colors.BorBrush, Child = c };
}
