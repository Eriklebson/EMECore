using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using EMECore.Core.Models;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class LibraryPage : UserControl
{
    public event EventHandler<Game>? GameSelected;
    public event EventHandler<Game>? GameLaunchRequested;
    public event EventHandler? ScanRequested;

    private List<Game> _allGames = new();
    private readonly TextBlock _countText;
    private readonly StackPanel _emptyState;
    private readonly GridView _gamesGridView;
    private readonly AutoSuggestBox _searchBox;
    private readonly Button _scanBtn;

    public LibraryPage()
    {
        var root = new Grid();

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid { Padding = new Thickness(24, 16, 24, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        titlePanel.Children.Add(new FontIcon { Glyph = "\uE80F", FontSize = 20, Foreground = SteamColors.BlueBrush });
        titlePanel.Children.Add(new TextBlock { Text = "Biblioteca", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = SteamColors.TextBrush, VerticalAlignment = VerticalAlignment.Center });
        _countText = new TextBlock { Text = "(0)", FontSize = 14, Foreground = SteamColors.TextSecondaryBrush, VerticalAlignment = VerticalAlignment.Center };
        titlePanel.Children.Add(_countText);

        _scanBtn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE721", FontSize = 14 },
                    new TextBlock { Text = "Procurar Jogos", VerticalAlignment = VerticalAlignment.Center }
                }
            },
            Background = SteamColors.BlueBrush,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(0, 0, 12, 0)
        };
        _scanBtn.Click += (_, _) => ScanRequested?.Invoke(this, EventArgs.Empty);
        titlePanel.Children.Add(_scanBtn);
        header.Children.Add(titlePanel);

        _searchBox = new AutoSuggestBox
        {
            PlaceholderText = "Pesquisar jogos...",
            Width = 240,
            QueryIcon = new FontIcon { Glyph = "\uE721" },
            Background = SteamColors.CardBrush,
            Foreground = SteamColors.TextBrush,
            BorderBrush = SteamColors.CardHoverBrush
        };
        _searchBox.TextChanged += SearchBox_TextChanged;
        Grid.SetColumn(_searchBox, 2);
        header.Children.Add(_searchBox);

        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var contentGrid = new Grid { Padding = new Thickness(16, 0, 16, 16) };

        _emptyState = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 12
        };
        _emptyState.Children.Add(new FontIcon { Glyph = "\uE7F3", FontSize = 64, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0x66, 0xc0, 0xf4)) });
        _emptyState.Children.Add(new TextBlock { Text = "Nenhum jogo encontrado", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SteamColors.TextBrush, HorizontalAlignment = HorizontalAlignment.Center });
        _emptyState.Children.Add(new TextBlock { Text = "Clique em \"Procurar Jogos\" para encontrar seus jogos instalados", FontSize = 13, Foreground = SteamColors.TextSecondaryBrush, HorizontalAlignment = HorizontalAlignment.Center });
        contentGrid.Children.Add(_emptyState);

        _gamesGridView = new GridView
        {
            SelectionMode = ListViewSelectionMode.None,
            Visibility = Visibility.Collapsed,
            Padding = new Thickness(0)
        };
        contentGrid.Children.Add(_gamesGridView);

        Grid.SetRow(contentGrid, 1);
        root.Children.Add(contentGrid);

        Content = root;
    }

    public void LoadGames(IList<Game> games)
    {
        _allGames = games.ToList();
        RefreshGrid(_allGames);
    }

    private void RefreshGrid(IList<Game> games)
    {
        _gamesGridView.Items.Clear();

        if (games.Count == 0)
        {
            _emptyState.Visibility = Visibility.Visible;
            _gamesGridView.Visibility = Visibility.Collapsed;
            _countText.Text = "(0)";
        }
        else
        {
            _emptyState.Visibility = Visibility.Collapsed;
            _gamesGridView.Visibility = Visibility.Visible;
            _countText.Text = $"({games.Count})";

            foreach (var game in games)
            {
                var card = new GameCard();
                card.SetGame(game);
                card.GameClicked += Card_GameClicked;
                card.PlayRequested += Card_PlayRequested;
                _gamesGridView.Items.Add(card);
            }
        }
    }

    public void SetScanning(bool scanning)
    {
        var panel = (StackPanel)_scanBtn.Content;
        var text = (TextBlock)panel.Children[1];
        text.Text = scanning ? "Procurando..." : "Procurar Jogos";
        _scanBtn.IsEnabled = !scanning;
    }

    private void Card_GameClicked(object? sender, Game game)
    {
        GameSelected?.Invoke(this, game);
    }

    private void Card_PlayRequested(object? sender, Game game)
    {
        GameLaunchRequested?.Invoke(this, game);
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var query = sender.Text.ToLower();
            if (string.IsNullOrWhiteSpace(query))
                RefreshGrid(_allGames);
            else
                RefreshGrid(_allGames.Where(g => g.Name.ToLower().Contains(query)).ToList());
        }
    }
}
