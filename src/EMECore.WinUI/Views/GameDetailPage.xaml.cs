using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    private readonly TextBlock _platformText;
    private readonly TextBlock _playTimeText;
    private readonly TextBlock _lastPlayedText;
    private readonly TextBlock _pathText;
    private readonly Image _coverImage;
    private readonly FontIcon _coverPlaceholder;

    public GameDetailPage()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { Padding = new Thickness(24, 16, 24, 12), Background = SteamColors.DarkestBrush };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var backButton = new Button
        {
            Content = new FontIcon { Glyph = "\uE72B", FontSize = 14 },
            Width = 36,
            Height = 36,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Transparent),
            Foreground = SteamColors.TextBrush,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4)
        };
        backButton.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
        header.Children.Add(backButton);

        var coverImageBorder = new Border
        {
            Width = 72,
            Height = 45,
            CornerRadius = new CornerRadius(4),
            Background = SteamColors.CardHoverBrush,
            Margin = new Thickness(12, 0, 0, 0),
            Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry
            {
                Rect = new Windows.Foundation.Rect(0, 0, 72, 45)
            }
        };
        _coverImage = new Image
        {
            Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _coverPlaceholder = new FontIcon
        {
            Glyph = "\uE7F3",
            FontSize = 28,
            Foreground = SteamColors.BlueBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var coverGrid = new Grid();
        coverGrid.Children.Add(_coverImage);
        coverGrid.Children.Add(_coverPlaceholder);
        coverImageBorder.Child = coverGrid;
        Grid.SetColumn(coverImageBorder, 1);
        header.Children.Add(coverImageBorder);

        var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(12, 0, 0, 0) };
        _gameTitle = new TextBlock { FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = SteamColors.TextBrush, VerticalAlignment = VerticalAlignment.Center };
        titlePanel.Children.Add(_gameTitle);
        Grid.SetColumn(titlePanel, 2);
        header.Children.Add(titlePanel);

        var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var launchBtn = new Button
        {
            Content = "Jogar",
            Background = SteamColors.GreenBrush,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Padding = new Thickness(24, 8, 24, 8),
            CornerRadius = new CornerRadius(4),
            FontSize = 14
        };
        launchBtn.Click += (_, _) => { if (_game != null) LaunchRequested?.Invoke(this, _game); };
        actionsPanel.Children.Add(launchBtn);

        var deleteBtn = new Button
        {
            Content = "Remover",
            Background = SteamColors.RedBrush,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White),
            Padding = new Thickness(16, 8, 16, 8),
            CornerRadius = new CornerRadius(4),
            FontSize = 12
        };
        deleteBtn.Click += (_, _) => { if (_game != null) DeleteRequested?.Invoke(this, _game); };
        actionsPanel.Children.Add(deleteBtn);
        Grid.SetColumn(actionsPanel, 3);
        header.Children.Add(actionsPanel);

        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var scrollViewer = new ScrollViewer { Padding = new Thickness(24) };
        var contentStack = new StackPanel { Spacing = 20, MaxWidth = 800 };

        var infoGrid = new Grid { ColumnSpacing = 16 };
        infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _platformText = new TextBlock { FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SteamColors.TextBrush };
        infoGrid.Children.Add(CreateInfoCard("Plataforma", _platformText, 0));

        _playTimeText = new TextBlock { FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SteamColors.GreenBrush };
        infoGrid.Children.Add(CreateInfoCard("Tempo de Jogo", _playTimeText, 1));

        _lastPlayedText = new TextBlock { FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SteamColors.TextBrush };
        infoGrid.Children.Add(CreateInfoCard("Ultima Vez", _lastPlayedText, 2));

        contentStack.Children.Add(infoGrid);

        _pathText = new TextBlock { FontSize = 12, Foreground = SteamColors.TextBrush, TextWrapping = TextWrapping.Wrap };
        contentStack.Children.Add(CreateInfoCard("Caminho", _pathText, 0));

        var sessionsStack = new StackPanel { Spacing = 8 };
        sessionsStack.Children.Add(new TextBlock { Text = "Sessoes de Jogo", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SteamColors.TextBrush });
        sessionsStack.Children.Add(new TextBlock { Text = "Nenhuma sessao registrada", FontSize = 12, Foreground = SteamColors.TextSecondaryBrush });
        var sessionsCard = new Border
        {
            Background = SteamColors.CardBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = sessionsStack
        };
        contentStack.Children.Add(sessionsCard);

        scrollViewer.Content = contentStack;
        Grid.SetRow(scrollViewer, 1);
        root.Children.Add(scrollViewer);

        Content = root;
    }

    private static Border CreateInfoCard(string label, UIElement value, int column)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = SteamColors.TextSecondaryBrush });
        stack.Children.Add(value);
        var card = new Border
        {
            Background = SteamColors.CardBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = stack
        };
        Grid.SetColumn(card, column);
        return card;
    }

    public void LoadGame(Game game)
    {
        _game = game;
        _gameTitle.Text = game.Name;
        _platformText.Text = game.Platform.ToUpper();

        var ts = TimeSpan.FromMinutes(game.PlayTime);
        _playTimeText.Text = game.PlayTime > 0
            ? (ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : $"{ts.Minutes}m")
            : "Nunca jogado";

        _lastPlayedText.Text = game.LastPlayed.HasValue
            ? game.LastPlayed.Value.ToString("dd/MM/yyyy HH:mm")
            : "Nunca";

        _pathText.Text = game.ExecutablePath;

        if (!string.IsNullOrWhiteSpace(game.CoverImage) &&
            Uri.TryCreate(game.CoverImage, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "file"))
        {
            var bitmap = new BitmapImage();
            bitmap.ImageOpened += (_, _) =>
            {
                _coverImage.Visibility = Visibility.Visible;
                _coverPlaceholder.Visibility = Visibility.Collapsed;
            };
            bitmap.ImageFailed += (_, _) =>
            {
                _coverImage.Visibility = Visibility.Collapsed;
                _coverPlaceholder.Visibility = Visibility.Visible;
            };
            bitmap.UriSource = uri;
            _coverImage.Source = bitmap;
        }
        else
        {
            _coverImage.Source = null;
            _coverImage.Visibility = Visibility.Collapsed;
            _coverPlaceholder.Visibility = Visibility.Visible;
        }
    }
}
