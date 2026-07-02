using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using EMECore.Core.Models;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class GameDetailPage : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler<Game>? LaunchRequested;
    public event EventHandler<Game>? DeleteRequested;

    private Game? _game;
    private readonly TextBlock _gameTitle;
    private readonly TextBlock _developerText;
    private readonly TextBlock _platformBadge;
    private readonly TextBlock _playTimeValue;
    private readonly TextBlock _lastPlayedValue;
    private readonly TextBlock _pathValue;
    private readonly TextBlock _achievementsTitle;
    private readonly StackPanel _achievementsContainer;
    private readonly StackPanel _sessionsContainer;
    private readonly Image _heroImage;
    private readonly Border _coverThumb;

    public GameDetailPage()
    {
        var root = new Grid();

        _heroImage = new Image { Stretch = Stretch.UniformToFill, Visibility = Visibility.Collapsed };
        root.Children.Add(_heroImage);

        var overlay = new Grid { Background = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(0, 1),
            GradientStops =
            {
                new GradientStop { Color = Windows.UI.Color.FromArgb(0xDD, 0x0e, 0x16, 0x21), Offset = 0 },
                new GradientStop { Color = Windows.UI.Color.FromArgb(0xBB, 0x0e, 0x16, 0x21), Offset = 0.3 },
                new GradientStop { Color = Windows.UI.Color.FromArgb(0xEE, 0x0e, 0x16, 0x21), Offset = 1 }
            }
        }};
        root.Children.Add(overlay);

        var scrollViewer = new ScrollViewer();
        var contentRoot = new StackPanel();

        // Header
        var header = new Grid { Margin = new Thickness(24, 24, 24, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var backBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE72B", FontSize = 14 },
            Width = 40, Height = 40, Padding = new Thickness(0),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x55, 0x00, 0x00, 0x00)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(20)
        };
        backBtn.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);

        _coverThumb = new Border
        {
            Width = 100, Height = 140, CornerRadius = new CornerRadius(8),
            Child = new Image { Stretch = Stretch.UniformToFill }
        };
        var titleBlock = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Spacing = 4, Margin = new Thickness(16, 0, 0, 0) };
        _gameTitle = new TextBlock { FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.ExtraBlack, Foreground = new SolidColorBrush(Colors.White), TextWrapping = TextWrapping.Wrap };
        titleBlock.Children.Add(_gameTitle);
        _developerText = new TextBlock { FontSize = 14, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xBB, 0xFF, 0xFF, 0xFF)) };
        titleBlock.Children.Add(_developerText);
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 6, 0, 0) };
        var badgeBorder = new Border { Background = SteamColors.BlueBrush, CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 4, 10, 4) };
        _platformBadge = new TextBlock { FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) };
        badgeBorder.Child = _platformBadge;
        badgeRow.Children.Add(badgeBorder);
        titleBlock.Children.Add(badgeRow);

        var leftHeader = new StackPanel { Orientation = Orientation.Horizontal };
        leftHeader.Children.Add(backBtn);
        leftHeader.Children.Add(_coverThumb);
        leftHeader.Children.Add(titleBlock);
        Grid.SetColumn(leftHeader, 0);
        header.Children.Add(leftHeader);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Top };
        var playBtn = new Button
        {
            Content = BtnContent("\uE768", "Jogar"),
            Background = SteamColors.GreenBrush, Foreground = new SolidColorBrush(Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold, CornerRadius = new CornerRadius(6),
            Padding = new Thickness(24, 10, 24, 10), FontSize = 15
        };
        playBtn.Click += (_, _) => { if (_game != null) LaunchRequested?.Invoke(this, _game); };
        actions.Children.Add(playBtn);

        var delBtn = new Button
        {
            Content = BtnContent("\uE74D", "Remover"),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0xFF, 0x44, 0x44)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x66, 0x66)),
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 10, 16, 10)
        };
        delBtn.Click += (_, _) => { if (_game != null) DeleteRequested?.Invoke(this, _game); };
        actions.Children.Add(delBtn);
        Grid.SetColumn(actions, 2);
        header.Children.Add(actions);

        contentRoot.Children.Add(header);

        // Info strip
        var infoStrip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, Margin = new Thickness(24, 4, 24, 6) };
        _playTimeValue = new TextBlock { FontSize = 13, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)) };
        _pathValue = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)), TextTrimming = TextTrimming.CharacterEllipsis };
        infoStrip.Children.Add(_playTimeValue);
        infoStrip.Children.Add(_pathValue);
        contentRoot.Children.Add(infoStrip);

        // Content cards
        var cardsStack = new StackPanel { Spacing = 14, Margin = new Thickness(24, 8, 24, 24) };

        var achCard = new StackPanel { Spacing = 8 };
        _achievementsTitle = new TextBlock { Text = "Conquistas (0/0)", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = SteamColors.TextBrush };
        achCard.Children.Add(_achievementsTitle);
        _achievementsContainer = new StackPanel { Spacing = 2 };
        achCard.Children.Add(_achievementsContainer);
        cardsStack.Children.Add(GlassCard(achCard));

        var infoCard = new StackPanel { Spacing = 8 };
        infoCard.Children.Add(SidebarTitle("Informacoes"));
        _lastPlayedValue = InfoText(infoCard, "Ultima Vez", "Nunca");
        cardsStack.Children.Add(GlassCard(infoCard));

        var sessCard = new StackPanel { Spacing = 6 };
        sessCard.Children.Add(SidebarTitle("Sessoes Recentes"));
        _sessionsContainer = new StackPanel { Spacing = 4 };
        _sessionsContainer.Children.Add(new TextBlock { Text = "Nenhuma sessao registrada", FontSize = 11, Foreground = SteamColors.TextSecondaryBrush });
        sessCard.Children.Add(_sessionsContainer);
        cardsStack.Children.Add(GlassCard(sessCard));

        contentRoot.Children.Add(cardsStack);
        scrollViewer.Content = contentRoot;
        root.Children.Add(scrollViewer);
        Content = root;
    }

    private static StackPanel BtnContent(string g, string t) => new()
    {
        Orientation = Orientation.Horizontal, Spacing = 6,
        Children = { new FontIcon { Glyph = g, FontSize = 14 }, new TextBlock { Text = t, VerticalAlignment = VerticalAlignment.Center } }
    };

    private static Border GlassCard(UIElement child) => new()
    {
        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x99, 0x17, 0x2A, 0x40)),
        CornerRadius = new CornerRadius(12), Padding = new Thickness(16),
        BorderThickness = new Thickness(1),
        BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF)),
        Child = child
    };

    private static TextBlock SidebarTitle(string text) => new()
    {
        Text = text, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SteamColors.TextSecondaryBrush
    };

    private static TextBlock InfoText(StackPanel parent, string label, string value)
    {
        var row = new StackPanel { Spacing = 1 };
        row.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = SteamColors.TextSecondaryBrush });
        var val = new TextBlock { Text = value, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SteamColors.TextBrush };
        row.Children.Add(val);
        parent.Children.Add(row);
        return val;
    }

    public void LoadGame(Game game)
    {
        _game = game;
        _gameTitle.Text = game.Name;
        _platformBadge.Text = game.Platform.ToUpper();
        _developerText.Text = game.Platform == "steam" ? "Steam" : game.Platform == "xbox" ? "Xbox / Game Pass" : "Outro";

        var ts = TimeSpan.FromMinutes(game.PlayTime);
        _playTimeValue.Text = game.PlayTime > 0 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : "Nunca jogado";

        _lastPlayedValue.Text = game.LastPlayed.HasValue ? game.LastPlayed.Value.ToString("dd/MM/yyyy HH:mm") : "Nunca";

        _pathValue.Text = game.ExecutablePath.Length > 80 ? "..." + game.ExecutablePath[^77..] : game.ExecutablePath;

        if (!string.IsNullOrWhiteSpace(game.CoverImage) &&
            Uri.TryCreate(game.CoverImage, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "file"))
        {
            _heroImage.Source = new BitmapImage(uri);
            _heroImage.Visibility = Visibility.Visible;
            ((Image)_coverThumb.Child).Source = new BitmapImage(uri);
        }
    }

    public void SetAchievements(List<Achievement> achievements)
    {
        _achievementsContainer.Children.Clear();

        if (achievements.Count == 0)
        {
            _achievementsContainer.Children.Add(new TextBlock { Text = "Nenhuma conquista", FontSize = 12, Foreground = SteamColors.TextSecondaryBrush });
            return;
        }

        var achieved = achievements.Count(a => a.Achieved);
        _achievementsTitle.Text = $"Conquistas ({achieved}/{achievements.Count})";

        var barBg = new Border { Background = SteamColors.CardHoverBrush, CornerRadius = new CornerRadius(3), Height = 5, Margin = new Thickness(0, 2, 0, 2) };
        var barFill = new Border { Background = SteamColors.BlueBrush, CornerRadius = new CornerRadius(3), Height = 5, Width = achievements.Count > 0 ? 400 * achieved / achievements.Count : 0, HorizontalAlignment = HorizontalAlignment.Left };
        var bar = new Grid(); bar.Children.Add(barBg); bar.Children.Add(barFill);
        _achievementsContainer.Children.Add(bar);

        var pct = new TextBlock { Text = $"{achieved}/{achievements.Count} — {Math.Round(achievements.Count > 0 ? 100.0 * achieved / achievements.Count : 0, 0)}%", FontSize = 11, Foreground = SteamColors.TextSecondaryBrush, Margin = new Thickness(0, 2, 0, 4) };
        _achievementsContainer.Children.Add(pct);

        foreach (var ach in achievements)
        {
            var done = ach.Achieved;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(4, 3, 4, 3) };
            row.Children.Add(new FontIcon
            {
                Glyph = done ? "\uE8FB" : "\uE739", FontSize = 14,
                Foreground = done ? SteamColors.BlueBrush : new SolidColorBrush(Windows.UI.Color.FromArgb(0x44, 0x66, 0x66, 0x66))
            });
            row.Children.Add(new TextBlock
            {
                Text = ach.Name, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = done ? SteamColors.TextBrush : new SolidColorBrush(Windows.UI.Color.FromArgb(0x77, 0xC6, 0xD4, 0xDF)),
                VerticalAlignment = VerticalAlignment.Center
            });
            _achievementsContainer.Children.Add(row);
        }
    }
}
