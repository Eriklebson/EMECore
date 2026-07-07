using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using EMECore.Core.Models;
using EMECore.Hardware.Services;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class GameDetailPage : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler<Game>? LaunchRequested;
    public event EventHandler<Game>? DeleteRequested;

    private Game? _game;
    private readonly TextBlock _title, _playTime, _lastPlayed, _origin, _category, _path;
    private readonly StackPanel _achievePanel, _reqPanel;
    private readonly Image _hero;
    private readonly Border _cover;
    private readonly StellarBladeAchievementImageService _imgSvc = new();

    public GameDetailPage()
    {
        var root = new Grid();
        _hero = new Image { Stretch=Stretch.UniformToFill, Visibility=Visibility.Collapsed, Opacity=0.3, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center }; root.Children.Add(_hero);
        root.Children.Add(new Grid { Background=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(0,1),GradientStops={new GradientStop{Color=Windows.UI.Color.FromArgb(0x99,0x0A,0x0B,0x0D),Offset=0},new GradientStop{Color=Windows.UI.Color.FromArgb(0xD9,0x0A,0x0B,0x0D),Offset=0.45},new GradientStop{Color=Windows.UI.Color.FromArgb(0xFF,0x0A,0x0B,0x0D),Offset=1}}} });

        var scroll = new ScrollViewer(); var c = new StackPanel();

        // Breadcrumb
        var back = new Button { Content=new StackPanel{Orientation=Orientation.Horizontal,Spacing=Design.S.SM,Children={new FontIcon{Glyph="\uE72B",FontSize=14},new TextBlock{Text="Biblioteca",FontSize=12}}}, Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent), Foreground=Design.C.MutedB, BorderThickness=new Thickness(1), BorderBrush=Design.C.BorB, CornerRadius=Design.R.MD, Padding=new Thickness(Design.S.MD,6,Design.S.MD,6), Margin=new Thickness(0,Design.S.LG,0,Design.S.XX) };
        back.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty); c.Children.Add(back);

        // Hero grid
        var hg = new Grid { Margin=new Thickness(Design.S.XXL,0,Design.S.XXL,Design.S.HU), ColumnSpacing=Design.S.XXL };
        hg.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(220)});
        hg.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});

        _cover = new Border { Width=220, Height=275, CornerRadius=Design.R.XXL, BorderThickness=new Thickness(1), BorderBrush=Design.C.BorB, Child=new Image{Stretch=Stretch.UniformToFill}, VerticalAlignment=VerticalAlignment.Top };
        Grid.SetColumn(_cover,0); hg.Children.Add(_cover);

        var info = new StackPanel { VerticalAlignment=VerticalAlignment.Top };
        var badgeRow = new StackPanel { Orientation=Orientation.Horizontal, Spacing=Design.S.MD, Margin=new Thickness(0,0,0,Design.S.MD) };
        var platTag = new Border { Background=new SolidColorBrush(Design.C.OtherBg), CornerRadius=Design.R.SM, Padding=new Thickness(6,2,6,2), BorderThickness=new Thickness(1), BorderBrush=Design.C.BorB, Child=Design.T.Badge() };
        ((TextBlock)platTag.Child).Foreground=Design.C.MutedB;
        badgeRow.Children.Add(platTag);
        badgeRow.Children.Add(new TextBlock{Text="Sem categoria",FontSize=12,Foreground=Design.C.MutedB,VerticalAlignment=VerticalAlignment.Center});
        info.Children.Add(badgeRow);

        _title = Design.T.H1(); info.Children.Add(_title);
        info.Children.Add(new TextBlock{Text="",FontSize=14,Foreground=Design.C.MutedB,Margin=new Thickness(0,Design.S.SM,0,Design.S.XX)});

        var btns = new StackPanel { Orientation=Orientation.Horizontal, Spacing=Design.S.MD, Margin=new Thickness(0,0,0,Design.S.XXL) };
        var play = new Button { Content=new StackPanel{Orientation=Orientation.Horizontal,Spacing=Design.S.SM,Children={new FontIcon{Glyph="\uE768",FontSize=16},new TextBlock{Text="Jogar",FontSize=14}}}, Background=Design.C.PriB, Foreground=Design.C.BgB, FontWeight=Microsoft.UI.Text.FontWeights.SemiBold, CornerRadius=Design.R.MD, Padding=new Thickness(Design.S.XX,10,Design.S.XX,10), BorderThickness=new Thickness(1), BorderBrush=Design.C.PriB };
        play.Click += (_, _) => { if(_game!=null)LaunchRequested?.Invoke(this,_game); }; btns.Children.Add(play);
        var del = new Button { Content=new StackPanel{Orientation=Orientation.Horizontal,Spacing=Design.S.SM,Children={new FontIcon{Glyph="\uE74D",FontSize=16},new TextBlock{Text="Remover",FontSize=14}}}, Background=new SolidColorBrush(Windows.UI.Color.FromArgb(0x1F,0xE8,0x4D,0x4D)), Foreground=new SolidColorBrush(Design.C.Danger), FontWeight=Microsoft.UI.Text.FontWeights.SemiBold, CornerRadius=Design.R.MD, Padding=new Thickness(Design.S.LG,10,Design.S.LG,10) };
        del.Click += (_, _) => { if(_game!=null)DeleteRequested?.Invoke(this,_game); }; btns.Children.Add(del);
        info.Children.Add(btns);

        // Stats
        var stats = new Grid { ColumnSpacing=Design.S.XX };
        stats.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        stats.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        stats.Children.Add(Metric("\uE917","Tempo de Jogo",out _playTime));
        var ps = Metric("\uE8B7","Caminho",out _path); _path.FontSize=12; _path.FontFamily=new("Consolas"); Grid.SetColumn(ps,1); stats.Children.Add(ps);
        info.Children.Add(stats);

        Grid.SetColumn(info,1); hg.Children.Add(info); c.Children.Add(hg);

        // Two-column
        var cg2 = new Grid { Margin=new Thickness(Design.S.XXL,0,Design.S.XXL,Design.S.XXL), ColumnSpacing=Design.S.XXL };
        cg2.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        cg2.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(320)});

        // Left: Achievements
        var achSec = new StackPanel { Spacing=Design.S.MD };
        var achHdr = new StackPanel { Orientation=Orientation.Horizontal, Spacing=Design.S.SM };
        achHdr.Children.Add(new FontIcon{Glyph="\uE8FB",FontSize=14,Foreground=Design.C.PriB});
        var achLbl = Design.T.Label(); achLbl.Text="Conquistas"; achLbl.Foreground=Design.C.Muted70B; achHdr.Children.Add(achLbl);
        achSec.Children.Add(achHdr);
        _achievePanel = new StackPanel { Spacing=Design.S.SM }; achSec.Children.Add(_achievePanel);
        var achCard = new Border { Background=Design.C.Card60B, CornerRadius=Design.R.XXL, Padding=new Thickness(Design.S.XL), BorderThickness=new Thickness(1), BorderBrush=Design.C.BorB, Child=achSec };
        Grid.SetColumn(achCard,0); cg2.Children.Add(achCard);

        // Right
        var right = new StackPanel { Spacing=Design.S.XL };

        var infoCard = new Border { Background=Design.C.Card60B, CornerRadius=Design.R.XXL, Padding=new Thickness(Design.S.XL), BorderThickness=new Thickness(1), BorderBrush=Design.C.BorB };
        var ic = new StackPanel { Spacing=Design.S.MD };
        var icHdr = new StackPanel { Orientation=Orientation.Horizontal, Spacing=Design.S.SM };
        icHdr.Children.Add(new FontIcon{Glyph="\uE946",FontSize=14,Foreground=Design.C.PriB});
        var icLbl = Design.T.Label(); icLbl.Text="Informações"; icLbl.Foreground=Design.C.Muted70B; icHdr.Children.Add(icLbl);
        ic.Children.Add(icHdr);
        var ifields = new StackPanel { Spacing=Design.S.SM };
        ifields.Children.Add(InfoRow("Última Vez",out _lastPlayed));
        ifields.Children.Add(InfoRow("Origem",out _origin));
        ifields.Children.Add(InfoRow("Categoria",out _category));
        ic.Children.Add(ifields);
        infoCard.Child=ic; right.Children.Add(infoCard);

        var sessCard = new Border { Background=Design.C.Card60B, CornerRadius=Design.R.XXL, Padding=new Thickness(Design.S.XL), BorderThickness=new Thickness(1), BorderBrush=Design.C.BorB };
        var sc = new StackPanel { Spacing=Design.S.MD };
        var sh = new StackPanel { Orientation=Orientation.Horizontal, Spacing=Design.S.SM };
        sh.Children.Add(new FontIcon{Glyph="\uE917",FontSize=14,Foreground=Design.C.PriB});
        var sl = Design.T.Label(); sl.Text="Sessões Recentes"; sl.Foreground=Design.C.Muted70B; sh.Children.Add(sl);
        sc.Children.Add(sh);
        sc.Children.Add(new TextBlock{Text="Nenhuma sessão registrada",FontSize=12,Foreground=Design.C.MutedB});
        sessCard.Child=sc; right.Children.Add(sessCard);

        var reqCard = new Border { Background=Design.C.Card60B, CornerRadius=Design.R.XXL, Padding=new Thickness(Design.S.XL), BorderThickness=new Thickness(1), BorderBrush=Design.C.BorB };
        var rc = new StackPanel { Spacing=Design.S.MD };
        var rh = new StackPanel { Orientation=Orientation.Horizontal, Spacing=Design.S.SM };
        rh.Children.Add(new FontIcon{Glyph="\uEDA2",FontSize=14,Foreground=Design.C.PriB});
        var rl = Design.T.Label(); rl.Text="Requisitos do Sistema"; rl.Foreground=Design.C.Muted70B; rh.Children.Add(rl);
        rc.Children.Add(rh);
        _reqPanel = new StackPanel { Spacing=Design.S.LG };
        _reqPanel.Children.Add(new TextBlock{Text="Carregando requisitos...",FontSize=12,Foreground=Design.C.MutedB});
        rc.Children.Add(_reqPanel);
        reqCard.Child=rc; right.Children.Add(reqCard);

        Grid.SetColumn(right,1); cg2.Children.Add(right);
        c.Children.Add(cg2);

        scroll.Content=c; root.Children.Add(scroll);
        Content=root;
    }

    private static StackPanel Metric(string icon, string label, out TextBlock value)
    {
        var s = new StackPanel { Spacing=Design.S.XS };
        var h = new StackPanel { Orientation=Orientation.Horizontal, Spacing=6 };
        h.Children.Add(new FontIcon{Glyph=icon,FontSize=12,Foreground=Design.C.MutedB});
        var l = Design.T.Label(); l.Text=label; l.Foreground=Design.C.MutedB; h.Children.Add(l);
        s.Children.Add(h);
        value = new TextBlock{Text="-",FontSize=14,Foreground=Design.C.FgB}; s.Children.Add(value);
        return s;
    }

    private static StackPanel InfoRow(string label, out TextBlock value)
    {
        var s = new StackPanel { Orientation=Orientation.Horizontal, Spacing=Design.S.LG };
        s.Children.Add(new TextBlock{Text=label,FontSize=12,Foreground=Design.C.MutedB,Width=80});
        value = new TextBlock{Text="-",FontSize=12,Foreground=Design.C.FgB}; s.Children.Add(value);
        return s;
    }

    public void LoadGame(Game game)
    {
        _game=game; _title.Text=game.Name;
        var ts=TimeSpan.FromMinutes(game.PlayTime);
        _playTime.Text=game.PlayTime>0?$"{(int)ts.TotalHours}h {ts.Minutes}m":"Nunca jogado";
        _lastPlayed.Text=game.LastPlayed.HasValue?game.LastPlayed.Value.ToString("dd/MM/yyyy HH:mm"):"Nunca";
        _origin.Text=game.Platform.ToUpper();
        _category.Text="Sem categoria";
        _path.Text=game.ExecutablePath.Length>80?"..."+game.ExecutablePath[^77..]:game.ExecutablePath;
        if(!string.IsNullOrWhiteSpace(game.CoverImage)&&Uri.TryCreate(game.CoverImage,UriKind.Absolute,out var u)&&(u.Scheme=="http"||u.Scheme=="https"||u.Scheme=="file"))
        {_hero.Source=new BitmapImage(u);_hero.Visibility=Visibility.Visible;((Image)_cover.Child).Source=new BitmapImage(u);}
    }

    public void SetRequirements(SteamRequirements? r, string plat="")
    {
        _reqPanel.Children.Clear();
        if(r==null||(string.IsNullOrEmpty(r.Minimum)&&string.IsNullOrEmpty(r.Recommended))){_reqPanel.Children.Add(new TextBlock{Text="Requisitos não disponíveis",FontSize=12,Foreground=Design.C.MutedB});return;}
        if(!string.IsNullOrEmpty(r.Minimum)){var s=new StackPanel{Spacing=Design.S.SM};s.Children.Add(new TextBlock{Text="Mínimos",FontSize=12,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,Foreground=Design.C.FgB});s.Children.Add(new TextBlock{Text=Clean(r.Minimum),FontSize=11,Foreground=Design.C.FgB,TextWrapping=TextWrapping.Wrap});_reqPanel.Children.Add(s);}
        if(!string.IsNullOrEmpty(r.Recommended)){var s=new StackPanel{Spacing=Design.S.SM};s.Children.Add(new TextBlock{Text="Recomendados",FontSize=12,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,Foreground=Design.C.FgB});s.Children.Add(new TextBlock{Text=Clean(r.Recommended),FontSize=11,Foreground=Design.C.FgB,TextWrapping=TextWrapping.Wrap});_reqPanel.Children.Add(s);}
    }
    private static string Clean(string h){var c=System.Text.RegularExpressions.Regex.Replace(h,"<[^>]*>","");return System.Text.RegularExpressions.Regex.Replace(c,@"\s+"," ").Trim();}

    public async Task SetAchievements(List<Achievement> achievements)
    {
        _achievePanel.Children.Clear();
        if(achievements.Count==0){_achievePanel.Children.Add(new TextBlock{Text="Nenhuma conquista",FontSize=12,Foreground=Design.C.MutedB});return;}
        var done=achievements.Count(a=>a.Achieved);var pct=(double)done/achievements.Count;

        // ═══════ PROGRESS BAR GERAL ═══════
        var progBar = new Grid { Margin=new Thickness(0,0,0,Design.S.SM) };
        progBar.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(Math.Clamp(pct*100,0,100),GridUnitType.Star)});
        progBar.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(Math.Clamp(100-pct*100,0,100),GridUnitType.Star)});
        var barFill = new Border{Background=Design.C.PriB,CornerRadius=new CornerRadius(2),Height=4};
        var barTrack = new Border{Background=Design.C.SecB,CornerRadius=new CornerRadius(2),Height=4};
        Grid.SetColumn(barFill,0); Grid.SetColumn(barTrack,1);
        progBar.Children.Add(barFill); progBar.Children.Add(barTrack);
        _achievePanel.Children.Add(progBar);

        var headerRow = new Grid { Margin=new Thickness(0,0,0,Design.S.MD) };
        headerRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        headerRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        headerRow.Children.Add(new TextBlock{Text=$"{done} de {achievements.Count} conquistas desbloqueadas",FontSize=12,Foreground=Design.C.MutedB});
        var pctBadge = new Border{Background=Design.C.Pri5B,CornerRadius=Design.R.MD,Padding=new Thickness(Design.S.MD,6,Design.S.MD,6),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(Design.S.LG,0,0,0),Child=new TextBlock{Text=$"{Math.Round(pct*100)}%",FontSize=24,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,Foreground=Design.C.PriB}};
        Grid.SetColumn(pctBadge,1); headerRow.Children.Add(pctBadge);
        _achievePanel.Children.Add(headerRow);

        // Separator
        _achievePanel.Children.Add(new Border{Background=Design.C.BorB,Height=1,Margin=new Thickness(0,0,0,Design.S.MD)});

        var grid=new Grid{ColumnSpacing=Design.S.MD,RowSpacing=Design.S.SM};
        grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        for(int i=0;i<achievements.Count;i++)
        {
            var a=achievements[i];var d=a.Achieved;
            var card=new Border{Background=d?Design.C.Pri5B:Design.C.InsetB,CornerRadius=Design.R.XL,Padding=new Thickness(Design.S.MD),BorderThickness=new Thickness(1),BorderBrush=d?new SolidColorBrush(Design.C.PriRing):Design.C.BorB};
            var ac=new Grid{ColumnSpacing=Design.S.MD};
            ac.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            ac.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            var icn=new Border{Width=40,Height=40,CornerRadius=Design.R.LG,Background=d?Design.C.Pri10B:Design.C.SecB,VerticalAlignment=VerticalAlignment.Center,BorderThickness=new Thickness(1),BorderBrush=d?new SolidColorBrush(Design.C.PriRing):Design.C.BorB};
            // Carregar imagem da conquista
            var achImage = new Image { Width=32, Height=32, Stretch=Stretch.UniformToFill, HorizontalAlignment=HorizontalAlignment.Center, VerticalAlignment=VerticalAlignment.Center };
            var imgPath = await _imgSvc.GetAchievementImageAsync(a.Apiname);
            if (!string.IsNullOrEmpty(imgPath))
                achImage.Source = new BitmapImage(new Uri($"file:///{imgPath}"));
            else
                icn.Child = new FontIcon{Glyph="\uE8FB",FontSize=20,Foreground=d?Design.C.PriB:Design.C.MutedB,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            if (!string.IsNullOrEmpty(imgPath)) icn.Child = achImage;
            ac.Children.Add(icn);
            var ts=new StackPanel{VerticalAlignment=VerticalAlignment.Center,Spacing=Design.S.XS};
            ts.Children.Add(new TextBlock{Text=a.Name,FontSize=14,FontWeight=Microsoft.UI.Text.FontWeights.Medium,Foreground=d?Design.C.FgB:Design.C.Muted70B,TextTrimming=TextTrimming.CharacterEllipsis});
            if(!string.IsNullOrEmpty(a.Description)) ts.Children.Add(new TextBlock{Text=a.Description,FontSize=12,Foreground=Design.C.MutedB,MaxLines=2,TextWrapping=TextWrapping.Wrap});

            // Progress bar mini (HTML: "Progresso 1050 / 1500" + barra h-1)
            if(a.HasProgress && !d)
            {
                var apc = new StackPanel{Spacing=Design.S.XS,Margin=new Thickness(0,Design.S.XS,0,0)};
                var progLabel = new StackPanel{Orientation=Orientation.Horizontal,Spacing=Design.S.XS};
                progLabel.Children.Add(new TextBlock{Text="Progresso",FontSize=10,Foreground=Design.C.MutedB});
                progLabel.Children.Add(new TextBlock{Text=$"{a.Progress} / {a.MaxProgress}",FontSize=10,FontFamily=new("Consolas"),Foreground=Design.C.MutedB});
                apc.Children.Add(progLabel);
                var pp = a.MaxProgress>0?Math.Min(100,(double)a.Progress/a.MaxProgress*100):0;
                var pb = new Grid();
                pb.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(pp,GridUnitType.Star)});
                pb.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(Math.Max(0,100-pp),GridUnitType.Star)});
                var mpFill = new Border{Background=Design.C.PriB,CornerRadius=new CornerRadius(2),Height=4};
                var mpTrack = new Border{Background=Design.C.SecB,CornerRadius=new CornerRadius(2),Height=4};
                Grid.SetColumn(mpFill,0); Grid.SetColumn(mpTrack,1);
                pb.Children.Add(mpFill); pb.Children.Add(mpTrack);
                apc.Children.Add(pb);
                ts.Children.Add(apc);
            }
            else
                ts.Children.Add(new TextBlock{Text=d?"Desbloqueado":"Bloqueado",FontSize=10,FontWeight=Microsoft.UI.Text.FontWeights.SemiBold,Foreground=d?Design.C.PriB:Design.C.Muted70B});
            Grid.SetColumn(ts,1);ac.Children.Add(ts);card.Child=ac;
            Grid.SetColumn(card,i%2);Grid.SetRow(card,i/2);grid.Children.Add(card);
        }
        for(int i=0;i<(achievements.Count+1)/2;i++)grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        _achievePanel.Children.Add(grid);
    }
}
