using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class Sidebar : UserControl
{
    public event EventHandler<string>? NavigationRequested;
    public event EventHandler? ScanRequested;

    private readonly Button _scanBtn;
    private readonly TextBlock _scanText;
    private readonly TextBlock _statusTextBlock;
    private readonly TextBlock _statsText;
    private readonly TextBlock _playTimeText;

    public Sidebar()
    {
        var root = new Grid { Background = SteamColors.DarkerBrush };

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Logo Area
        var logoPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        logoPanel.Children.Add(new FontIcon { Glyph = "\uE7F3", FontSize = 24, Foreground = SteamColors.BlueBrush });
        var logoTexts = new StackPanel();
        logoTexts.Children.Add(new TextBlock { Text = "E.M.E Core", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = SteamColors.TextBrush });
        logoTexts.Children.Add(new TextBlock { Text = "v2.0.1.0", FontSize = 10, Foreground = SteamColors.TextSecondaryBrush });
        logoPanel.Children.Add(logoTexts);
        var logoBorder = new Grid { Padding = new Thickness(16, 20, 16, 12) };
        logoBorder.Children.Add(logoPanel);
        Grid.SetRow(logoBorder, 0);
        root.Children.Add(logoBorder);

        // Navigation
        var navPanel = new StackPanel { Padding = new Thickness(0, 8, 0, 8) };

        var navLibraryBtn = CreateSidebarButton("\uE80F", "Biblioteca");
        navLibraryBtn.Click += (_, _) => NavigationRequested?.Invoke(this, "library");
        navPanel.Children.Add(navLibraryBtn);

        var addBtn = CreateSidebarButton("\uE710", "Adicionar Jogo");
        addBtn.Click += (_, _) => NavigationRequested?.Invoke(this, "addgame");
        navPanel.Children.Add(addBtn);

        _scanBtn = CreateSidebarButton("\uE721", "Procurar Jogos");
        _scanText = (TextBlock)((StackPanel)_scanBtn.Content).Children[1];
        _scanBtn.Click += (_, _) => ScanRequested?.Invoke(this, EventArgs.Empty);
        navPanel.Children.Add(_scanBtn);

        // Status card
        var statusCard = new Border
        {
            Background = SteamColors.CardBrush,
            Margin = new Thickness(16, 16, 16, 0),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12)
        };
        var statusStack = new StackPanel { Spacing = 4 };
        statusStack.Children.Add(new TextBlock { Text = "Status", FontSize = 11, Foreground = SteamColors.TextSecondaryBrush, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        _statusTextBlock = new TextBlock { Text = "Pronto", FontSize = 12, Foreground = SteamColors.TextBrush, TextWrapping = TextWrapping.Wrap };
        statusStack.Children.Add(_statusTextBlock);
        statusCard.Child = statusStack;
        navPanel.Children.Add(statusCard);

        Grid.SetRow(navPanel, 1);
        root.Children.Add(navPanel);

        // Stats footer
        var footerPanel = new StackPanel { Spacing = 2 };
        _statsText = new TextBlock { Text = "0 jogos", FontSize = 11, Foreground = SteamColors.TextSecondaryBrush };
        _playTimeText = new TextBlock { Text = "0m jogado", FontSize = 11, Foreground = SteamColors.TextSecondaryBrush };
        footerPanel.Children.Add(_statsText);
        footerPanel.Children.Add(_playTimeText);
        var footer = new Border
        {
            Background = SteamColors.DarkestBrush,
            Padding = new Thickness(16, 12, 16, 12),
            Child = footerPanel
        };
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        Content = root;
    }

    private static Button CreateSidebarButton(string glyph, string label)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        panel.Children.Add(new FontIcon { Glyph = glyph, FontSize = 16 });
        panel.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });

        return new Button
        {
            Background = new SolidColorBrush(Colors.Transparent),
            Foreground = SteamColors.TextBrush,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(16, 12, 16, 12),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            Height = 44,
            Content = panel
        };
    }

    public void UpdateStats(string stats, string playTime, string status)
    {
        _statsText.Text = stats;
        _playTimeText.Text = playTime;
        _statusTextBlock.Text = status;
    }

    public void SetScanning(bool scanning)
    {
        _scanText.Text = scanning ? "Procurando..." : "Procurar Jogos";
        _scanBtn.IsEnabled = !scanning;
    }
}
