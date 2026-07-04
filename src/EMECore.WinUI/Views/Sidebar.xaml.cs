using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class Sidebar : UserControl
{
    public event EventHandler<string>? NavigationRequested;
    public event EventHandler? MonitorRequested;
    public event EventHandler? FishingMacroRequested;
    public event EventHandler? TestAchievementRequested;

    private readonly TextBlock _statusTextBlock;
    private readonly TextBlock _statsText;
    private readonly TextBlock _playTimeText;
    private readonly TextBlock _gamesCountBadge;
    private Button? _activeButton;
    private Button? _fishBtn;
    private Button? _testAchBtn;

    public Sidebar()
    {
        var root = new Grid { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xF5, 0x17, 0x1A, 0x21)) };

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Logo Section
        var logoBorder = new Border
        {
            Padding = new Thickness(16, 20, 16, 16),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF))
        };
        var logoPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        
        var logoImage = new Image
        {
            Width = 36,
            Height = 36,
            VerticalAlignment = VerticalAlignment.Center
        };
        var logoBitmap = new BitmapImage(new Uri("ms-appx:///Assets/Logo/logo.png"));
        logoImage.Source = logoBitmap;
        logoPanel.Children.Add(logoImage);
        
        var logoTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        logoTexts.Children.Add(new TextBlock 
        { 
            Text = "E.M.E Core", 
            FontSize = 15, 
            FontWeight = Microsoft.UI.Text.FontWeights.Bold, 
            Foreground = SteamColors.TextBrush 
        });
        logoTexts.Children.Add(new TextBlock 
        { 
            Text = "v2.10.2.0", 
            FontSize = 10, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x66, 0x8B, 0x9B, 0xB4)),
            FontFamily = new FontFamily("Consolas")
        });
        logoPanel.Children.Add(logoTexts);
        logoBorder.Child = logoPanel;
        Grid.SetRow(logoBorder, 0);
        root.Children.Add(logoBorder);

        // Navigation Section
        var navPanel = new StackPanel { Padding = new Thickness(12, 16, 12, 8) };
        
        // Section Label
        navPanel.Children.Add(new TextBlock
        {
            Text = "NAVEGAÇÃO",
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x50, 0x8B, 0x9B, 0xB4)),
            Margin = new Thickness(4, 0, 0, 8),
            CharacterSpacing = 50
        });

        // Library Button with count badge
        var libraryBtn = CreateSidebarButton("\uE80F", "Biblioteca", true);
        libraryBtn.Click += (_, _) => 
        {
            NavigationRequested?.Invoke(this, "library");
            SetActiveButton(libraryBtn);
        };
        
        // Add count badge to library button
        var libraryPanel = (StackPanel)libraryBtn.Content;
        _gamesCountBadge = new TextBlock
        {
            Text = "0",
            FontSize = 11,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x99, 0x66, 0xC0, 0xF4)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0)
        };
        libraryPanel.Children.Add(_gamesCountBadge);
        navPanel.Children.Add(libraryBtn);

        var addBtn = CreateSidebarButton("\uE710", "Adicionar Jogo", false);
        addBtn.Click += (_, _) => 
        {
            NavigationRequested?.Invoke(this, "addgame");
            SetActiveButton(addBtn);
        };
        navPanel.Children.Add(addBtn);

        navPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF)),
            Margin = new Thickness(4, 12, 4, 12)
        });

        // Section Label for Tools
        navPanel.Children.Add(new TextBlock
        {
            Text = "FERRAMENTAS",
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x50, 0x8B, 0x9B, 0xB4)),
            Margin = new Thickness(4, 0, 0, 8),
            CharacterSpacing = 50
        });

        var monBtn = CreateSidebarButton("\uE9CA", "Monitor de Hardware", false);
        monBtn.Click += (_, _) => 
        {
            MonitorRequested?.Invoke(this, EventArgs.Empty);
            SetActiveButton(monBtn);
        };
        navPanel.Children.Add(monBtn);

        // Test Achievement Button
        _testAchBtn = CreateSidebarButton("\uE8FB", "Testar Conquista", false);
        _testAchBtn.Click += (_, _) => TestAchievementRequested?.Invoke(this, EventArgs.Empty);
        navPanel.Children.Add(_testAchBtn);

        // Fishing Macro Button (hidden by default, shown only for Stellar Blade)
        _fishBtn = CreateSidebarButton("\uE8FB", "Macro de Pesca", false);
        _fishBtn.IsEnabled = false;
        _fishBtn.Visibility = Visibility.Collapsed;
        _fishBtn.Click += (_, _) => FishingMacroRequested?.Invoke(this, EventArgs.Empty);
        navPanel.Children.Add(_fishBtn);

        // Status Card
        var statusCard = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x1E, 0x2A, 0x3A)),
            Margin = new Thickness(12, 16, 12, 0),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF))
        };
        var statusStack = new StackPanel { Spacing = 6 };
        statusStack.Children.Add(new TextBlock 
        { 
            Text = "STATUS", 
            FontSize = 9, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0x8B, 0x9B, 0xB4)),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            CharacterSpacing = 50
        });
        _statusTextBlock = new TextBlock 
        { 
            Text = "Pronto", 
            FontSize = 12, 
            Foreground = SteamColors.TextBrush, 
            TextWrapping = TextWrapping.Wrap 
        };
        statusStack.Children.Add(_statusTextBlock);
        statusCard.Child = statusStack;
        navPanel.Children.Add(statusCard);

        Grid.SetRow(navPanel, 1);
        root.Children.Add(navPanel);

        // Footer Section
        var footerPanel = new StackPanel { Spacing = 4 };
        
        var statsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _statsText = new TextBlock 
        { 
            Text = "0 jogos", 
            FontSize = 11, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x8B, 0x9B, 0xB4)) 
        };
        _playTimeText = new TextBlock 
        { 
            Text = "0m jogado", 
            FontSize = 11, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x8B, 0x9B, 0xB4)) 
        };
        statsRow.Children.Add(_statsText);
        statsRow.Children.Add(new TextBlock 
        { 
            Text = "•", 
            FontSize = 11, 
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x8B, 0x9B, 0xB4)) 
        });
        statsRow.Children.Add(_playTimeText);
        footerPanel.Children.Add(statsRow);
        
        var footer = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x0E, 0x16, 0x21)),
            Padding = new Thickness(16, 14, 16, 14),
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF)),
            Child = footerPanel
        };
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
        
        // Set library as active by default
        SetActiveButton(libraryBtn);
    }

    private static Button CreateSidebarButton(string glyph, string label, bool isActive)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        panel.Children.Add(new FontIcon 
        { 
            Glyph = glyph, 
            FontSize = 16,
            Foreground = isActive 
                ? SteamColors.BlueBrush 
                : SteamColors.TextSecondaryBrush
        });
        panel.Children.Add(new TextBlock 
        { 
            Text = label, 
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        return new Button
        {
            Background = isActive 
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x18, 0x66, 0xC0, 0xF4))
                : new SolidColorBrush(Colors.Transparent),
            Foreground = isActive ? SteamColors.BlueBrush : SteamColors.TextBrush,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(12, 10, 12, 10),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            Height = 40,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(2, 2, 2, 2),
            Content = panel
        };
    }

    private void SetActiveButton(Button button)
    {
        if (_activeButton != null)
        {
            _activeButton.Background = new SolidColorBrush(Colors.Transparent);
            _activeButton.Foreground = SteamColors.TextBrush;
            var panel = (StackPanel)_activeButton.Content;
            var icon = (FontIcon)panel.Children[0];
            icon.Foreground = SteamColors.TextSecondaryBrush;
        }

        _activeButton = button;
        _activeButton.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x18, 0x66, 0xC0, 0xF4));
        _activeButton.Foreground = SteamColors.BlueBrush;
        var activePanel = (StackPanel)_activeButton.Content;
        var activeIcon = (FontIcon)activePanel.Children[0];
        activeIcon.Foreground = SteamColors.BlueBrush;
    }

    public void UpdateStats(string stats, string playTime, string status)
    {
        _statsText.Text = stats;
        _playTimeText.Text = playTime;
        _statusTextBlock.Text = status;
        
        // Update games count badge
        var countText = stats.Replace(" jogos", "").Replace(" jogo", "");
        if (int.TryParse(countText, out var count))
        {
            _gamesCountBadge.Text = count.ToString();
        }
    }

    public void UpdateFishingMacroVisibility(bool isStellarBlade)
    {
        if (_fishBtn != null)
        {
            _fishBtn.Visibility = isStellarBlade ? Visibility.Visible : Visibility.Collapsed;
            _fishBtn.IsEnabled = isStellarBlade;
        }
    }
}
