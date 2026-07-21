using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EMECore.Core.Services;
using EMECore.WinUI.Controls;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class Sidebar : UserControl
{
    public event EventHandler<string>? NavigationRequested;
    public event EventHandler? MonitorRequested;
    public event EventHandler? TestAchievementRequested;
    public event EventHandler<bool>? CollapseChanged;
    public event EventHandler? SettingsRequested;

    private bool _collapsed;
    private readonly Grid _root;
    private readonly StackPanel _logo, _nav;
    private readonly Microsoft.UI.Xaml.Controls.Image _logoImage;
    private readonly TextBlock _navLbl, _utilLbl;
    private readonly Border _indicator;
    private readonly SidebarItem _libraryBtn;
    private readonly SidebarItem _settingsBtn;
    private readonly SidebarItem _monBtn;
    private readonly SidebarItem _toolsBtn;
    private readonly SidebarItem _trainBtn;
    private readonly Button _collapseBtn;
    private readonly WebView2 _adWeb;
    private readonly List<SidebarItem> _items = new();

    public Sidebar()
    {
        _root = new Grid { Background = Design.C.SideB, BorderThickness = new Thickness(0, 0, 1, 0), BorderBrush = Design.C.BorB };
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var logoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Design.S.MD };
        var logoImage = new Microsoft.UI.Xaml.Controls.Image
        {
            Width = 40,
            Height = 40,
            VerticalAlignment = VerticalAlignment.Center
        };
        var logoBitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/Logo/logo.png"));
        logoImage.Source = logoBitmap;
        _logoImage = logoImage;
        logoRow.Children.Add(logoImage);
        _logo = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        _logo.Children.Add(new TextBlock { Text = "E.M.E Core", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Design.C.FgB });
        _logo.Children.Add(new TextBlock { Text = "v2.26.0.0", FontSize = 10, Foreground = Design.C.Muted70B, FontFamily = new("Consolas"), CharacterSpacing = 100 });
        logoRow.Children.Add(_logo);
        var lb = new Border { Padding = new Thickness(Design.S.XL), Child = logoRow };
        Grid.SetRow(lb, 0); _root.Children.Add(lb);

        _collapseBtn = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Design.S.SM, Children = { new FontIcon { Glyph = "\uE76B", FontSize = 14, FontFamily = new FontFamily("Segoe MDL2 Assets"), Foreground = Design.C.MutedB }, new TextBlock { Text = "Recolher", FontSize = 11, Foreground = Design.C.MutedB, VerticalAlignment = VerticalAlignment.Center, CharacterSpacing = 30 } } },
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(Design.S.MD, 6, Design.S.MD, 6), Margin = new Thickness(Design.S.MD, 0, Design.S.MD, Design.S.LG),
            CornerRadius = Design.R.MD
        };
        _collapseBtn.Click += (_, _) =>
        {
            _collapsed = !_collapsed;
            ApplyCollapsedState(_collapsed);
            SettingsService.Set("sidebar_collapsed", _collapsed.ToString());
            CollapseChanged?.Invoke(this, _collapsed);
        };
        Grid.SetRow(_collapseBtn, 1); _root.Children.Add(_collapseBtn);

        _nav = new StackPanel();
        _navLbl = SectionLabel("Navegação"); _nav.Children.Add(_navLbl);

        _libraryBtn = AddItem("\uE80F", "Jogos");
        _libraryBtn.Click += (_, _) => { Activate(_libraryBtn); NavigationRequested?.Invoke(this, "library"); };
        _toolsBtn = AddItem("\uE70F", "Ferramentas");
        _toolsBtn.Click += (_, _) => { Activate(_toolsBtn); NavigationRequested?.Invoke(this, "tools"); };
        _trainBtn = AddItem("\uE9D9", "Treinamento");
        _trainBtn.Click += (_, _) => { Activate(_trainBtn); NavigationRequested?.Invoke(this, "training"); };
        _settingsBtn = AddItem("\uE713", "Configurações");
        _settingsBtn.Click += (_, _) => { Activate(_settingsBtn); SettingsRequested?.Invoke(this, EventArgs.Empty); };

        var divC = new Grid { Height = Design.S.LG, Margin = new Thickness(Design.S.XL, Design.S.MD, Design.S.XL, Design.S.MD) };
        divC.Children.Add(new Border { Background = Design.C.BorB, Width = 1, HorizontalAlignment = HorizontalAlignment.Center });
        _nav.Children.Add(divC);

        _utilLbl = SectionLabel("Utilitários EME"); _nav.Children.Add(_utilLbl);

        _monBtn = AddItem("\uE9CA", "Monitor de Hardware");
        _monBtn.Click += (_, _) => MonitorRequested?.Invoke(this, EventArgs.Empty);

        var achBtn = AddItem("\uE8FB", "Testar Conquista");
        achBtn.Click += (_, _) => TestAchievementRequested?.Invoke(this, EventArgs.Empty);

        _indicator = new Border { Width=3, HorizontalAlignment=HorizontalAlignment.Left, CornerRadius=new CornerRadius(0,3,3,0), Background=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(0,1),GradientStops={new(){Color=Design.C.Pri,Offset=0},new(){Color=Design.C.Pri,Offset=1}}}, VerticalAlignment=VerticalAlignment.Top };
        var nc = new Grid(); nc.Children.Add(_indicator); nc.Children.Add(_nav);
        Grid.SetRow(nc, 2); _root.Children.Add(nc);

        _adWeb = new WebView2
        {
            Width = 180,
            Height = 320,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, Design.S.SM, 0, Design.S.SM),
            DefaultBackgroundColor = Microsoft.UI.Colors.Transparent
        };
        _adWeb.Loaded += async (_, _) =>
        {
            try
            {
                await _adWeb.EnsureCoreWebView2Async();
                var adPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "ad.html");
                if (System.IO.File.Exists(adPath))
                {
                    var uri = new Uri(adPath);
                    _adWeb.CoreWebView2.Navigate(uri.AbsoluteUri);
                }
            }
            catch { }
        };
        Grid.SetRow(_adWeb, 3); _root.Children.Add(_adWeb);

        var addPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Design.S.SM };
        addPanel.Children.Add(new FontIcon { Glyph = "\uE710", FontSize = 18, Foreground = Design.C.PriB });
        addPanel.Children.Add(new TextBlock { Text = "Adicionar Jogo", VerticalAlignment = VerticalAlignment.Center, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var addBtn = new Button { Content=addPanel, Background=Design.C.Pri10B, Foreground=Design.C.PriB, HorizontalContentAlignment=HorizontalAlignment.Left, HorizontalAlignment=HorizontalAlignment.Stretch, Padding=new Thickness(Design.S.MD,10,Design.S.MD,10), CornerRadius=Design.R.LG, BorderThickness=new Thickness(1), BorderBrush=new SolidColorBrush(Design.C.PriRing) };
        addBtn.Click += (_, _) => NavigationRequested?.Invoke(this, "addgame");
        addBtn.PointerEntered += (_, _) => { addBtn.Background = Design.C.PriB; addBtn.Foreground = Design.C.BgB; };
        addBtn.PointerExited += (_, _) => { addBtn.Background = Design.C.Pri10B; addBtn.Foreground = Design.C.PriB; };
        var fb = new Border { Background = Design.C.InsetB, Child = addBtn, Padding = new Thickness(Design.S.MD), BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Design.C.BorB };
        Grid.SetRow(fb, 4); _root.Children.Add(fb);

        Content = _root;
        Activate(_libraryBtn);
    }

    public void SetActiveCategory(string category)
    {
        var item = category switch
        {
            "library" or "game" => _libraryBtn,
            "tools" or "tool" => _toolsBtn,
            "training" => _trainBtn,
            "settings" => _settingsBtn,
            _ => _libraryBtn
        };
        Activate(item);
    }

    public void SetCollapsed(bool collapsed)
    {
        _collapsed = collapsed;
        ApplyCollapsedState(collapsed);
    }

    private void ApplyCollapsedState(bool collapsed)
    {
        ((FontIcon)((StackPanel)_collapseBtn.Content).Children[0]).Glyph = collapsed ? "\uE76C" : "\uE76B";
        ((TextBlock)((StackPanel)_collapseBtn.Content).Children[1]).Text = collapsed ? "" : "Recolher";
        _collapseBtn.HorizontalContentAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _logoImage.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        _logo.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        _navLbl.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        _utilLbl.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        _indicator.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        foreach (var i in _items) i.SetCollapsed(collapsed);
        _adWeb.Width = collapsed ? 56 : 180;
        _adWeb.Height = collapsed ? 100 : 320;
    }

    private SidebarItem AddItem(string g, string l) { var i = new SidebarItem(g, l); _items.Add(i); _nav.Children.Add(i); return i; }
    private void Activate(SidebarItem item) { foreach (var i in _items) i.IsActive = false; item.IsActive = true; var t = item.TransformToVisual(_nav); _indicator.Margin = new Thickness(0, (int)(t.TransformPoint(new Windows.Foundation.Point(0, 0)).Y + 6), 0, 0); }
    private static TextBlock SectionLabel(string t) => new() { Text = t, FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Design.C.Muted70B, Padding = new Thickness(Design.S.XL, 0, Design.S.XL, 0), Margin = new Thickness(0, 0, 0, Design.S.SM), CharacterSpacing = 180 };
    public void UpdateStats(string s, string t, string st) { }
    public void RefreshTheme()
    {
        _root.Background = new SolidColorBrush(ThemeManager.Current.Surface);
        foreach (var item in _items)
            item.RefreshTheme();
    }
}
