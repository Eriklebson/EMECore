using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EMECore.Core.Models;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class LibraryPage : UserControl
{
    public event EventHandler<Game>? GameSelected;
    public event EventHandler<Game>? GameLaunchRequested;
    public event EventHandler? ScanRequested;

    private List<Game> _allGames = new();
    private readonly StackPanel _emptyState;
    private readonly GridView _gamesGridView;
    private readonly AutoSuggestBox _searchBox;
    private readonly Button _scanBtn;

    public LibraryPage()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header Section
        var header = new Border
        {
            Padding = new Thickness(24, 20, 24, 16),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x0E, 0x16, 0x21)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF))
        };
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Title Section
        var titlePanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            Spacing = 12, 
            VerticalAlignment = VerticalAlignment.Center 
        };
        
        var titleIcon = new FontIcon 
        { 
            Glyph = "\uE80F", 
            FontSize = 22, 
            Foreground = SteamColors.BlueBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        titlePanel.Children.Add(titleIcon);
        
        var titleText = new TextBlock 
        { 
            Text = "Biblioteca", 
            FontSize = 20, 
            FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
            Foreground = SteamColors.TextBrush, 
            VerticalAlignment = VerticalAlignment.Center 
        };
        titlePanel.Children.Add(titleText);
        headerGrid.Children.Add(titlePanel);

        // Scan Button
        _scanBtn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE721", FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "Procurar Jogos", VerticalAlignment = VerticalAlignment.Center, FontSize = 13 }
                }
            },
            Background = new SolidColorBrush(Colors.Transparent),
            Foreground = SteamColors.TextBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 10, 16, 10),
            Margin = new Thickness(0, 0, 12, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x30, 0x1E, 0x2A, 0x3A))
        };
        _scanBtn.Click += (_, _) => ScanRequested?.Invoke(this, EventArgs.Empty);
        Grid.SetColumn(_scanBtn, 1);
        headerGrid.Children.Add(_scanBtn);

        // Search Box
        var searchContainer = new Grid
        {
            Width = 260,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        var searchIcon = new FontIcon
        {
            Glyph = "\uE721",
            FontSize = 14,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x8B, 0x9B, 0xB4)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        
        _searchBox = new AutoSuggestBox
        {
            PlaceholderText = "Pesquisar jogos...",
            QueryIcon = new FontIcon { Visibility = Visibility.Collapsed },
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x1E, 0x2A, 0x3A)),
            Foreground = SteamColors.TextBrush,
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _searchBox.TextChanged += SearchBox_TextChanged;
        
        searchContainer.Children.Add(_searchBox);
        searchContainer.Children.Add(searchIcon);
        Grid.SetColumn(searchContainer, 2);
        headerGrid.Children.Add(searchContainer);

        header.Child = headerGrid;
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // Content Section
        var contentGrid = new Grid 
        { 
            Padding = new Thickness(16, 12, 16, 16),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x1B, 0x28, 0x38))
        };

        // Empty State
        _emptyState = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 16
        };
        
        var emptyIconContainer = new Border
        {
            Width = 80,
            Height = 80,
            CornerRadius = new CornerRadius(20),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x18, 0x66, 0xC0, 0xF4)),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        emptyIconContainer.Child = new FontIcon 
        { 
            Glyph = "\uE7F3", 
            FontSize = 40, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x66, 0xC0, 0xF4)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _emptyState.Children.Add(emptyIconContainer);
        
        var emptyTitle = new TextBlock 
        { 
            Text = "Nenhum jogo encontrado", 
            FontSize = 18, 
            FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
            Foreground = SteamColors.TextBrush, 
            HorizontalAlignment = HorizontalAlignment.Center 
        };
        _emptyState.Children.Add(emptyTitle);
        
        var emptyDescription = new TextBlock 
        { 
            Text = "Clique em \"Procurar Jogos\" para encontrar seus jogos instalados", 
            FontSize = 13, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x8B, 0x9B, 0xB4)), 
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300
        };
        _emptyState.Children.Add(emptyDescription);
        contentGrid.Children.Add(_emptyState);

        // Games Grid
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
        }
        else
        {
            _emptyState.Visibility = Visibility.Collapsed;
            _gamesGridView.Visibility = Visibility.Visible;

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
        var icon = (FontIcon)panel.Children[0];
        
        text.Text = scanning ? "Procurando..." : "Procurar Jogos";
        icon.Glyph = scanning ? "\uE71A" : "\uE721";
        _scanBtn.IsEnabled = !scanning;
        _scanBtn.Opacity = scanning ? 0.7 : 1.0;
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
