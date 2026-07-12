using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using EMECore.Core.Models;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class GameCard : UserControl
{
    public event EventHandler<Game>? GameClicked;
    public event EventHandler<Game>? PlayRequested;
    private Game? _game;
    private readonly Image _cover;
    private readonly FontIcon _ph;
    private readonly TextBlock _platLbl, _name, _status, _genre;
    private readonly Border _badge, _hover, _dot, _coverBorder;

    public GameCard()
    {
        Width = 220; Padding = new Thickness(0);
        var o = new StackPanel();

        // COVER
        var cc = new Border { Height=275, CornerRadius=Design.R.XL, BorderThickness=new Thickness(1), BorderBrush=Design.C.BorB, Clip=new RectangleGeometry{Rect=new(0,0,220,275)}, Margin=new Thickness(0,0,0,Design.S.MD) };
        _coverBorder = cc;
        var cg = new Grid();
        _cover = new Image { Stretch=Stretch.UniformToFill, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, Visibility=Visibility.Collapsed }; cg.Children.Add(_cover);
        _ph = new FontIcon { Glyph="\uE7F3", FontSize=48, Foreground=Design.C.Pri5B, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center }; cg.Children.Add(_ph);
        cg.Children.Add(new Border { Background=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(0,1),GradientStops={new(){Color=Windows.UI.Color.FromArgb(0,0,0,0),Offset=0},new(){Color=Windows.UI.Color.FromArgb(0xD9,0x0A,0x0B,0x0D),Offset=1}}}, VerticalAlignment=VerticalAlignment.Bottom, Height=64 });

        // BADGE
        var bp = new StackPanel { Orientation=Orientation.Horizontal, Spacing=Design.S.XS, VerticalAlignment=VerticalAlignment.Center };
        bp.Children.Add(new Border { Width=6, Height=6, CornerRadius=new(3), Background=Design.C.PriB });
        _platLbl = Design.T.Badge(); bp.Children.Add(_platLbl);
        _badge = new Border { HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Top, CornerRadius=Design.R.SM, Padding=new Thickness(6,2,6,2), Margin=new Thickness(0,Design.S.SM,Design.S.SM,0), Child=bp }; cg.Children.Add(_badge);

        // HOVER overlay
        _hover = new Border { Background=new SolidColorBrush(Windows.UI.Color.FromArgb(0x80,0,0,0)), Opacity=0 };
        var ov = new StackPanel { HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center, Spacing=Design.S.SM };
        var pb = new Border { Background=Design.C.PriB, CornerRadius=Design.R.MD, Padding=new Thickness(Design.S.XL,10,Design.S.XL,10), Child=new StackPanel{Orientation=Orientation.Horizontal,Spacing=Design.S.SM,Children={new FontIcon{Glyph="\uE768",FontSize=16},new TextBlock{Text="Jogar",FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,FontSize=14,Foreground=Design.C.BgB}}} };
        pb.Tapped += (_, e) => { e.Handled=true; if(_game!=null)PlayRequested?.Invoke(this,_game); };
        ov.Children.Add(pb); _hover.Child=ov; cg.Children.Add(_hover);
        cc.Child=cg; o.Children.Add(cc);

        // TITLE
        _name = new TextBlock { FontSize=14, FontWeight=Microsoft.UI.Text.FontWeights.Medium, Foreground=Design.C.FgB, TextTrimming=TextTrimming.CharacterEllipsis, MaxLines=1, Margin=new Thickness(0,0,0,Design.S.XS) }; o.Children.Add(_name);

        // META
        var mr = new StackPanel { Orientation=Orientation.Horizontal, Spacing=Design.S.SM };
        _status = new TextBlock { FontSize=12, Foreground=Design.C.MutedB }; mr.Children.Add(_status);
        _dot = new Border { Width=4, Height=4, CornerRadius=new(2), Background=Design.C.Muted70B, VerticalAlignment=VerticalAlignment.Center }; mr.Children.Add(_dot);
        _genre = new TextBlock { FontSize=12, Foreground=Design.C.MutedB, TextTrimming=TextTrimming.CharacterEllipsis }; mr.Children.Add(_genre);
        o.Children.Add(mr);

        var w = new Border { Child=o, Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent), Margin=new Thickness(6) };
        w.PointerEntered += (_, _) => { _hover.Opacity=1; _name.Foreground=Design.C.PriB; _coverBorder.BorderBrush=new SolidColorBrush(Windows.UI.Color.FromArgb(0x80,0x4C,0xCB,0xA0)); };
        w.PointerExited += (_, _) => { _hover.Opacity=0; _name.Foreground=Design.C.FgB; _coverBorder.BorderBrush=Design.C.BorB; };
        w.Tapped += (_, e) =>
        {
            var src = e.OriginalSource as DependencyObject;
            while (src != null && src != pb && src != w) src = VisualTreeHelper.GetParent(src);
            if (src == pb) return;
            if(_game!=null)GameClicked?.Invoke(this,_game);
        };
        Content=w;
    }

    public void SetGame(Game game)
    {
        _game=game; _name.Text=game.Name;
        _status.Text=game.PlayTime>0?$"Jogado {TimeSpan.FromMinutes(game.PlayTime).TotalHours:F1}h":"Nunca jogado";
        _genre.Text=!string.IsNullOrEmpty(game.Genre)?game.Genre:"";
        _platLbl.Text=game.Platform.ToUpper();
        var (bg,txt,ring) = Design.PlatformColors(game.Platform);
        _badge.Background=new SolidColorBrush(bg); _badge.BorderThickness=new Thickness(1); _badge.BorderBrush=new SolidColorBrush(ring);
        _platLbl.Foreground=new SolidColorBrush(txt);
        if(!string.IsNullOrWhiteSpace(game.CoverImage)&&Uri.TryCreate(game.CoverImage,UriKind.Absolute,out var u)&&(u.Scheme=="http"||u.Scheme=="https"||u.Scheme=="file"))
        {var b=new BitmapImage();b.ImageOpened+=(_,_)=>{_cover.Visibility=Visibility.Visible;_ph.Visibility=Visibility.Collapsed;};b.ImageFailed+=(_,_)=>{_cover.Visibility=Visibility.Collapsed;_ph.Visibility=Visibility.Visible;};b.UriSource=u;_cover.Source=b;}
        else{_cover.Source=null;_cover.Visibility=Visibility.Collapsed;_ph.Visibility=Visibility.Visible;}
    }
}
