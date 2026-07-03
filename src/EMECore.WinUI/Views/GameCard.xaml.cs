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
    private readonly TextBlock _gameName;
    private readonly TextBlock _playTimeLabel;
    private readonly TextBlock _lastPlayedLabel;
    private readonly TextBlock _platformBadge;
    private readonly Image _coverImage;
    private readonly FontIcon _coverPlaceholder;
    private readonly Border _hoverOverlay;
    private readonly Border _playButtonOverlay;
    private readonly Border _detailsButtonOverlay;
    private readonly Border _deleteButtonOverlay;

    public GameCard()
    {
        Width = 220;
        Height = 260;
        Padding = new Thickness(0);
        CornerRadius = new CornerRadius(12);

        var outerGrid = new Grid();
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(180) });
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Cover Image Section
        var coverBorder = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0.5, 0),
                EndPoint = new Windows.Foundation.Point(0.5, 1),
                GradientStops =
                {
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0xFF, 0x1E, 0x2A, 0x3A), Offset = 0 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0xFF, 0x0E, 0x16, 0x21), Offset = 1 }
                }
            },
            CornerRadius = new CornerRadius(12, 12, 0, 0),
            Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, 220, 180) }
        };
        
        var coverGrid = new Grid();

        _coverImage = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        coverGrid.Children.Add(_coverImage);

        _coverPlaceholder = new FontIcon
        {
            Glyph = "\uE7F3",
            FontSize = 48,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0x66, 0xC0, 0xF4)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        coverGrid.Children.Add(_coverPlaceholder);

        // Gradient overlay for better text readability
        var gradientOverlay = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(0, 1),
                GradientStops =
                {
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0x00, 0x00, 0x00, 0x00), Offset = 0.5 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0xCC, 0x00, 0x00, 0x00), Offset = 1 }
                }
            },
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = 80
        };
        coverGrid.Children.Add(gradientOverlay);

        // Platform Badge
        _platformBadge = new TextBlock 
        { 
            FontSize = 9, 
            Foreground = new SolidColorBrush(Colors.White), 
            FontWeight = Microsoft.UI.Text.FontWeights.Bold 
        };
        var badgeBorder = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x99, 0x00, 0x00, 0x00)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = new CornerRadius(0, 12, 0, 8),
            Padding = new Thickness(10, 5, 10, 5),
            Child = _platformBadge
        };
        coverGrid.Children.Add(badgeBorder);

        // Hover Overlay with action buttons
        _hoverOverlay = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x00, 0x00, 0x00)),
            Opacity = 0,
            CornerRadius = new CornerRadius(12, 12, 0, 0)
        };
        
        var overlayPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8
        };
        
        // Play Button Overlay
        _playButtonOverlay = new Border
        {
            Background = SteamColors.GreenBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 10, 20, 10),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE768", FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "Jogar", VerticalAlignment = VerticalAlignment.Center, FontWeight = Microsoft.UI.Text.FontWeights.Bold }
                }
            }
        };
        _playButtonOverlay.Tapped += (_, e) => 
        { 
            e.Handled = true;
            if (_game != null) PlayRequested?.Invoke(this, _game); 
        };
        overlayPanel.Children.Add(_playButtonOverlay);
        
        // Details Button Overlay
        _detailsButtonOverlay = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 8, 20, 8),
            Child = new TextBlock { Text = "Detalhes", FontSize = 12, Foreground = new SolidColorBrush(Colors.White) }
        };
        _detailsButtonOverlay.Tapped += (_, e) => 
        { 
            e.Handled = true;
            if (_game != null) GameClicked?.Invoke(this, _game); 
        };
        overlayPanel.Children.Add(_detailsButtonOverlay);
        
        // Delete Button Overlay
        _deleteButtonOverlay = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0xD9, 0x41, 0x26)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20, 8, 20, 8),
            Child = new FontIcon { Glyph = "\uE74D", FontSize = 14, Foreground = new SolidColorBrush(Colors.White) }
        };
        overlayPanel.Children.Add(_deleteButtonOverlay);
        
        _hoverOverlay.Child = overlayPanel;
        coverGrid.Children.Add(_hoverOverlay);

        coverBorder.Child = coverGrid;
        Grid.SetRow(coverBorder, 0);
        outerGrid.Children.Add(coverBorder);

        // Info Section
        var infoBorder = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x1E, 0x2A, 0x3A)),
            CornerRadius = new CornerRadius(0, 0, 12, 12),
            Padding = new Thickness(14, 12, 14, 14),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF))
        };
        
        var infoStack = new StackPanel { Spacing = 6 };

        _gameName = new TextBlock 
        { 
            FontSize = 13, 
            FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
            Foreground = SteamColors.TextBrush, 
            TextTrimming = TextTrimming.CharacterEllipsis, 
            MaxLines = 1 
        };
        infoStack.Children.Add(_gameName);

        var metaStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        
        _playTimeLabel = new TextBlock 
        { 
            FontSize = 11, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x8B, 0x9B, 0xB4)) 
        };
        metaStack.Children.Add(_playTimeLabel);
        
        metaStack.Children.Add(new TextBlock 
        { 
            Text = "•", 
            FontSize = 11, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x8B, 0x9B, 0xB4)) 
        });
        
        _lastPlayedLabel = new TextBlock 
        { 
            FontSize = 11, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x8B, 0x9B, 0xB4)),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        metaStack.Children.Add(_lastPlayedLabel);
        infoStack.Children.Add(metaStack);

        infoBorder.Child = infoStack;
        Grid.SetRow(infoBorder, 1);
        outerGrid.Children.Add(infoBorder);

        // Main container with hover effects
        var cardContainer = new Border
        {
            Child = outerGrid,
            Background = new SolidColorBrush(Colors.Transparent),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF)),
            Margin = new Thickness(4)
        };
        
        // Pointer events for hover effects
        cardContainer.PointerEntered += (_, _) =>
        {
            _hoverOverlay.Opacity = 1;
            cardContainer.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x30, 0x66, 0xC0, 0xF4));
        };
        
        cardContainer.PointerExited += (_, _) =>
        {
            _hoverOverlay.Opacity = 0;
            cardContainer.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));
        };
        
        cardContainer.Tapped += (_, _) => { if (_game != null) GameClicked?.Invoke(this, _game); };

        Content = cardContainer;
    }

    public void SetGame(Game game)
    {
        _game = game;
        _gameName.Text = game.Name;

        var ts = TimeSpan.FromMinutes(game.PlayTime);
        _playTimeLabel.Text = game.PlayTime > 0
            ? (ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : $"{ts.Minutes}m")
            : "Nunca jogado";

        _lastPlayedLabel.Text = game.LastPlayed.HasValue
            ? $"{game.LastPlayed.Value:dd/MM/yyyy}"
            : "";

        _platformBadge.Text = game.Platform.ToUpper();

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
