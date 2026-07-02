using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    public GameCard()
    {
        Width = 200;
        Height = 280;
        Padding = new Thickness(0);

        var outerGrid = new Grid();
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) });
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Cover area
        var coverBorder = new Border
        {
            Background = SteamColors.CardHoverBrush,
            CornerRadius = new CornerRadius(8, 8, 0, 0)
        };
        var coverGrid = new Grid();
        coverGrid.Children.Add(new FontIcon { Glyph = "\uE7F3", FontSize = 48, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0x66, 0xc0, 0xf4)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        _platformBadge = new TextBlock { FontSize = 10, Foreground = SteamColors.TextBrush, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var badgeBorder = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0x00, 0x00, 0x00)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = new CornerRadius(0, 8, 0, 8),
            Padding = new Thickness(8, 4, 8, 4),
            Child = _platformBadge
        };
        coverGrid.Children.Add(badgeBorder);
        coverBorder.Child = coverGrid;
        Grid.SetRow(coverBorder, 0);
        outerGrid.Children.Add(coverBorder);

        // Info area
        var infoGrid = new Grid { Padding = new Thickness(12, 10, 12, 10) };
        infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        infoGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        infoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _gameName = new TextBlock { FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SteamColors.TextBrush, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 };
        Grid.SetRow(_gameName, 0);
        infoGrid.Children.Add(_gameName);

        _playTimeLabel = new TextBlock { FontSize = 11, Foreground = SteamColors.TextSecondaryBrush, Margin = new Thickness(0, 4, 0, 0) };
        Grid.SetRow(_playTimeLabel, 1);
        infoGrid.Children.Add(_playTimeLabel);

        _lastPlayedLabel = new TextBlock { FontSize = 10, Foreground = SteamColors.TextSecondaryBrush, VerticalAlignment = VerticalAlignment.Bottom, TextWrapping = TextWrapping.Wrap, MaxLines = 2 };
        Grid.SetRow(_lastPlayedLabel, 2);
        infoGrid.Children.Add(_lastPlayedLabel);

        var playButton = new Button
        {
            Content = "Jogar",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = SteamColors.BlueBrush,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            CornerRadius = new CornerRadius(4),
            Height = 32,
            Margin = new Thickness(0, 8, 0, 0)
        };
        playButton.Click += (_, _) => { if (_game != null) PlayRequested?.Invoke(this, _game); };
        Grid.SetRow(playButton, 3);
        infoGrid.Children.Add(playButton);

        Grid.SetRow(infoGrid, 1);
        outerGrid.Children.Add(infoGrid);

        // Tap handler for the whole card
        var tapBorder = new Border { Child = outerGrid, Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Transparent) };
        tapBorder.Tapped += (_, _) => { if (_game != null) GameClicked?.Invoke(this, _game); };

        Content = tapBorder;
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
            ? $"Jogado por último: {game.LastPlayed.Value:dd/MM/yyyy}"
            : "";

        _platformBadge.Text = game.Platform.ToUpper();
    }
}
