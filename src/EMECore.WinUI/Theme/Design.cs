using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace EMECore.WinUI.Theme;

/// <summary>Design System — tokens extraídos do layout HTML pixel-perfect</summary>
public static class Design
{
    // ═══════════ CORES (HTML exactas) ═══════════
    public static class C
    {
        public static Color Bg       { get; private set; } = Make(0x0A, 0x0B, 0x0D);
        public static Color Sidebar  { get; private set; } = Make(0x16, 0x17, 0x19);
        public static Color Card     { get; private set; } = Make(0x2A, 0x2D, 0x31);
        public static Color Card60   { get; private set; } = Make(0x99, 0x2A, 0x2D, 0x31);
        public static Color Inset    { get; private set; } = Make(0x1B, 0x1D, 0x22);
        public static Color Fg       { get; private set; } = Make(0xE8, 0xE9, 0xEB);
        public static Color Muted    { get; private set; } = Make(0xA8, 0xAB, 0xB0);
        public static Color Muted70  { get; private set; } = Make(0xB3, 0xA8, 0xAB, 0xB0);
        public static Color Pri      { get; private set; } = Make(0x4C, 0xCB, 0xA0);
        public static Color Warn     { get; private set; } = Make(0xE6, 0xA0, 0x30);
        public static Color Danger   { get; private set; } = Make(0xE8, 0x4D, 0x4D);
        public static Color Sec      { get; private set; } = Make(0x3A, 0x3D, 0x43);
        public static Color Bor      { get; private set; } = Make(0x0F, 0xFF, 0xFF, 0xFF);
        public static Color BorH     { get; private set; } = Make(0x80, 0x4C, 0xCB, 0xA0);
        public static Color Pri10    { get; private set; } = Make(0x1A, 0x4C, 0xCB, 0xA0);
        public static Color Pri5     { get; private set; } = Make(0x0D, 0x4C, 0xCB, 0xA0);
        public static Color PriRing  { get; private set; } = Make(0x40, 0x4C, 0xCB, 0xA0);
        public static Color PriRing20{ get; private set; } = Make(0x33, 0x4C, 0xCB, 0xA0);
        public static readonly Color White10  = Make(0x1A, 0xFF, 0xFF, 0xFF);
        public static readonly Color White06  = Make(0x0F, 0xFF, 0xFF, 0xFF);
        // Platform
        public static readonly Color Steam    = Make(0x66, 0xC0, 0xF4);
        public static readonly Color SteamBg  = Make(0xD9, 0x1B, 0x28, 0x38);
        public static readonly Color Xbox     = Make(0x7F, 0xCE, 0x7F);
        public static readonly Color XboxBg   = Make(0x40, 0x0E, 0x7A, 0x0D);
        public static readonly Color Riot     = Make(0xFF, 0x9A, 0x9C);
        public static readonly Color RiotBg   = Make(0x40, 0xD1, 0x36, 0x39);
        public static readonly Color Rockstar = Make(0xF5, 0xB3, 0x01);
        public static readonly Color RockstarBg = Make(0x33, 0xF5, 0xB3, 0x01);
        public static readonly Color OtherBg  = Make(0xCC, 0x27, 0x27, 0x2A);
        public static readonly Color OtherTxt = Make(0xD4, 0xD4, 0xD8);

        public static readonly SolidColorBrush BgB    = new(Bg);
        public static readonly SolidColorBrush SideB  = new(Sidebar);
        public static readonly SolidColorBrush CardB  = new(Card);
        public static readonly SolidColorBrush Card60B= new(Card60);
        public static readonly SolidColorBrush InsetB = new(Inset);
        public static readonly SolidColorBrush FgB    = new(Fg);
        public static readonly SolidColorBrush MutedB = new(Muted);
        public static readonly SolidColorBrush Muted70B=new(Muted70);
        public static readonly SolidColorBrush PriB   = new(Pri);
        public static readonly SolidColorBrush Pri10B = new(Pri10);
        public static readonly SolidColorBrush Pri5B  = new(Pri5);
        public static readonly SolidColorBrush SecB   = new(Sec);
        public static readonly SolidColorBrush BorB   = new(Bor);
        public static readonly SolidColorBrush BorHB  = new(BorH);

        public static void RefreshColors(AppTheme theme)
        {
            Bg = theme.Background;
            Sidebar = theme.Surface;
            Card = theme.Card;
            Card60 = WithAlpha(theme.Card, 0x99);
            Inset = Blend(theme.Background, theme.Surface, 0.55);
            Fg = theme.TextPrimary;
            Muted = theme.TextSecondary;
            Muted70 = WithAlpha(theme.TextSecondary, 0xB3);
            Pri = theme.Accent;
            Warn = theme.Warning;
            Danger = theme.Danger;
            Sec = theme.CardHover;
            Bor = theme.Border;
            BorH = theme.BorderHover;
            Pri10 = WithAlpha(theme.Accent, 0x1A);
            Pri5 = WithAlpha(theme.Accent, 0x0D);
            PriRing = WithAlpha(theme.Accent, 0x40);
            PriRing20 = WithAlpha(theme.Accent, 0x33);

            BgB.Color = Bg; SideB.Color = Sidebar; CardB.Color = Card; Card60B.Color = Card60;
            InsetB.Color = Inset; FgB.Color = Fg; MutedB.Color = Muted; Muted70B.Color = Muted70;
            PriB.Color = Pri; Pri10B.Color = Pri10; Pri5B.Color = Pri5; SecB.Color = Sec;
            BorB.Color = Bor; BorHB.Color = BorH;
        }

        private static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

        private static Color Blend(Color first, Color second, double amount)
        {
            byte Mix(byte a, byte b) => (byte)Math.Round(a + ((b - a) * amount));
            return Color.FromArgb(255, Mix(first.R, second.R), Mix(first.G, second.G), Mix(first.B, second.B));
        }

        static Color Make(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);
        static Color Make(byte r, byte g, byte b) => Color.FromArgb(255, r, g, b);
    }

    // ═══════════ ESPAÇAMENTO (escala 4px) ═══════════
    public static class S { public const double XS=4, SM=8, MD=12, LG=16, XL=20, XX=24, XXL=32, HU=40, HG=48; }

    // ═══════════ RADIUS ═══════════
    public static class R { public static readonly CornerRadius SM=new(4), MD=new(6), LG=new(8), XL=new(12), XXL=new(16), F=new(999); }

    // ═══════════ TIPOGRAFIA ═══════════
    public static class T
    {
        public static TextBlock Badge() => new() { FontSize=9, FontFamily=new("Consolas"), FontWeight=Microsoft.UI.Text.FontWeights.Bold, CharacterSpacing=100 };
        public static TextBlock Label() => new() { FontSize=10, FontWeight=Microsoft.UI.Text.FontWeights.SemiBold, CharacterSpacing=80 };
        public static TextBlock Body() => new() { FontSize=12, FontWeight=Microsoft.UI.Text.FontWeights.Normal };
        public static TextBlock BodyM() => new() { FontSize=14, FontWeight=Microsoft.UI.Text.FontWeights.Medium };
        public static TextBlock Title() => new() { FontSize=14, FontWeight=Microsoft.UI.Text.FontWeights.Medium };
        public static TextBlock H4() => new() { FontSize=16, FontWeight=Microsoft.UI.Text.FontWeights.SemiBold };
        public static TextBlock H3() => new() { FontSize=18, FontWeight=Microsoft.UI.Text.FontWeights.SemiBold };
        public static TextBlock H1() => new() { FontSize=48, FontWeight=Microsoft.UI.Text.FontWeights.SemiBold, CharacterSpacing=-25, LineHeight=48 };
    }

    // ═══════════ ANIMAÇÕES ═══════════
    public static void FadeIn(UIElement el, double ms=200) { var a=new DoubleAnimation{From=0,To=1,Duration=TimeSpan.FromMilliseconds(ms),EnableDependentAnimation=true}; Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(a,"Opacity"); var sb=new Storyboard(); sb.Children.Add(a); Storyboard.SetTarget(a,el); sb.Begin(); }
    public static void SlideUp(UIElement el, double fromY=20, double ms=200) { el.Opacity=0; var t=new CompositeTransform(); el.RenderTransform=t; var a=new DoubleAnimation{From=fromY,To=0,Duration=TimeSpan.FromMilliseconds(ms),EnableDependentAnimation=true}; Storyboard.SetTargetProperty(a,"(UIElement.RenderTransform).(CompositeTransform.TranslateY)"); var sb=new Storyboard(); sb.Children.Add(a); Storyboard.SetTarget(a,el); el.Opacity=1; sb.Begin(); }

    // ═══════════ PLATAFORMA ═══════════
    public static (Color bg, Color txt, Color ring) PlatformColors(string p) => p.ToLower() switch
    {
        "steam"    => (C.SteamBg, C.Steam,   Color.FromArgb(0x4D,0x66,0xC0,0xF4)),
        "xbox"     => (C.XboxBg,  C.Xbox,    Color.FromArgb(0x4D,0x7F,0xCE,0x7F)),
        "rockstar" => (C.RockstarBg, C.Rockstar, Color.FromArgb(0x4D,0xF5,0xB3,0x01)),
        "riot"     => (C.RiotBg,  C.Riot,    Color.FromArgb(0x4D,0xFF,0x9A,0x9C)),
        "epic"     => (C.OtherBg, C.Fg,      C.White06),
        _          => (C.OtherBg, C.OtherTxt, C.White06)
    };
}
