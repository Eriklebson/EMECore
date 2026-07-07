using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using CommunityToolkit.WinUI.Controls;
using EMECore.Core.Models;
using EMECore.Hardware.Services;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class LibraryPage : UserControl
{
    public event EventHandler<Game>? GameSelected;
    public event EventHandler<Game>? GameLaunchRequested;
    public event EventHandler? ScanRequested;

    private List<Game> _allGames = new();
    private string _category = "game";
    private readonly WrapPanel _grid;
    private readonly TextBox _search;
    private readonly TextBlock _count;

    public LibraryPage()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var hdr = new Border { Height=64, Padding=new Thickness(Design.S.XXL,0,Design.S.XXL,0), Background=new SolidColorBrush(Windows.UI.Color.FromArgb(0xB3,0x0A,0x0B,0x0D)), BorderThickness=new Thickness(0,0,0,1), BorderBrush=Design.C.BorB };
        var hg = new Grid();
        hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleP = new StackPanel { Orientation=Orientation.Horizontal, Spacing=Design.S.SM, VerticalAlignment=VerticalAlignment.Center };
        titleP.Children.Add(new FontIcon { Glyph="\uE80F", FontSize=16, Foreground=Design.C.PriB });
        titleP.Children.Add(new TextBlock { Text="Jogos", FontSize=18, FontWeight=Microsoft.UI.Text.FontWeights.SemiBold, Foreground=Design.C.FgB });
        hg.Children.Add(titleP);

        var scan = new Button { Content=new StackPanel{Orientation=Orientation.Horizontal,Spacing=Design.S.SM,Children={new FontIcon{Glyph="\uE721",FontSize=14},new TextBlock{Text="Procurar Jogos",FontSize=12}}}, Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent), Foreground=Design.C.MutedB, FontWeight=Microsoft.UI.Text.FontWeights.Medium, CornerRadius=Design.R.MD, Padding=new Thickness(Design.S.MD,6,Design.S.MD,6), BorderThickness=new Thickness(1), BorderBrush=Design.C.BorB };
        scan.Click += (_, _) => ScanRequested?.Invoke(this, EventArgs.Empty);
        Grid.SetColumn(scan, 1); hg.Children.Add(scan);

        var sb = new Border { Width=224, Background=Design.C.CardB, CornerRadius=Design.R.MD, BorderThickness=new Thickness(1), BorderBrush=Design.C.BorB, Padding=new Thickness(Design.S.MD,6,Design.S.MD,6) };
        var sr = new StackPanel { Orientation=Orientation.Horizontal, Spacing=Design.S.SM };
        sr.Children.Add(new FontIcon { Glyph="\uE721", FontSize=14, Foreground=Design.C.MutedB });
        _search = new TextBox { PlaceholderText="Pesquisar jogos...", Width=180, Height=20, Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent), Foreground=Design.C.FgB, BorderThickness=new Thickness(0), FontSize=12 };
        _search.TextChanged += (_, _) => Refresh();
        sr.Children.Add(_search); sb.Child=sr;
        Grid.SetColumn(sb, 2); hg.Children.Add(sb);
        hdr.Child=hg; Grid.SetRow(hdr,0); root.Children.Add(hdr);

        var cg = new Grid { Margin=new Thickness(Design.S.XX,Design.S.MD,Design.S.XX,Design.S.XX) };
        cg.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var cr = new StackPanel { Orientation=Orientation.Horizontal, Spacing=Design.S.SM, Margin=new Thickness(0,0,0,Design.S.MD) };
        cr.Children.Add(new TextBlock { Text="Biblioteca", FontSize=16, FontWeight=Microsoft.UI.Text.FontWeights.SemiBold, Foreground=Design.C.FgB });
        _count = new TextBlock { Text="0 de 0", FontSize=13, Foreground=Design.C.MutedB, FontFamily=new("Consolas") };
        cr.Children.Add(_count); cg.Children.Add(cr); Grid.SetRow(cr,0);

        _grid = new WrapPanel { Orientation=Microsoft.UI.Xaml.Controls.Orientation.Horizontal, HorizontalSpacing=12, VerticalSpacing=12 };
        var scroll = new ScrollViewer { Content=_grid, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled, VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        cg.Children.Add(scroll); Grid.SetRow(scroll,1);
        Grid.SetRow(cg,1); root.Children.Add(cg);
        Content=root;
    }

    public void LoadGames(IList<Game> games) { _allGames=games.ToList(); Refresh(); }
    public void SetCategory(string c) { _category=c; Refresh(); }
    public void SetScanning(bool s) { }
    public void RefreshTheme() { }

    private void Refresh()
    {
        var f = _allGames.Where(g => GameScannerService.GetGameCategory(g.Name) == _category).ToList();
        if (!string.IsNullOrWhiteSpace(_search.Text)) f = f.Where(g => g.Name.Contains(_search.Text, StringComparison.OrdinalIgnoreCase)).ToList();
        _grid.Children.Clear();
        foreach (var g in f) { var c = new GameCard(); c.SetGame(g); c.GameClicked += (_, gm) => GameSelected?.Invoke(this, gm); c.PlayRequested += (_, gm) => GameLaunchRequested?.Invoke(this, gm); _grid.Children.Add(c); }
        _count.Text = $"{f.Count} de {_allGames.Count}";
    }
}
