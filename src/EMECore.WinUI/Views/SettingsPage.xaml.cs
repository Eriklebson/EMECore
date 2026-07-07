using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class SettingsPage : UserControl
{
    public event EventHandler? BackRequested;

    private readonly StackPanel _themePanel;
    private readonly List<Border> _themeCards = new();

    public SettingsPage()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header
        var header = new Border
        {
            Padding = new Thickness(24, 20, 24, 16),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x0E, 0x16, 0x21)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF))
        };
        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center
        };

        var backBtn = new Button
        {
            Content = "\uE72B",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Background = new SolidColorBrush(Colors.Transparent),
            Foreground = SteamColors.TextBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(8),
            VerticalAlignment = VerticalAlignment.Center
        };
        backBtn.Click += (_, _) => BackRequested?.Invoke(this, EventArgs.Empty);
        headerPanel.Children.Add(backBtn);

        headerPanel.Children.Add(new FontIcon
        {
            Glyph = "\uE713",
            FontSize = 22,
            Foreground = SteamColors.BlueBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        headerPanel.Children.Add(new TextBlock
        {
            Text = "Configurações",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = SteamColors.TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        header.Child = headerPanel;
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // Content
        var scroll = new ScrollViewer();
        var content = new StackPanel
        {
            Padding = new Thickness(24, 20, 24, 20),
            Spacing = 24
        };

        // Theme Section
        content.Children.Add(CreateSectionTitle("APARÊNCIA"));

        content.Children.Add(new TextBlock
        {
            Text = "Selecione o visual do E.M.E Core",
            FontSize = 13,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x8B, 0x9B, 0xB4)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        _themePanel = new StackPanel { Spacing = 12 };
        foreach (var theme in ThemeManager.AvailableThemes)
        {
            _themePanel.Children.Add(CreateThemeCard(theme));
        }
        content.Children.Add(_themePanel);

        // General Section
        content.Children.Add(CreateSectionTitle("GERAL"));

        var infoCard = CreateInfoCard("Sobre o E.M.E Core", "Versão 2.14.0.0 — Launcher de jogos com monitor de hardware");
        content.Children.Add(infoCard);

        scroll.Content = content;
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        Content = root;
        UpdateSelection();
    }

    private Border CreateThemeCard(AppTheme theme)
    {
        var isActive = theme.Name == ThemeManager.Current?.Name;

        var accentPreview = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(theme.Accent),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        textStack.Children.Add(new TextBlock
        {
            Text = theme.Name,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = SteamColors.TextBrush
        });
        textStack.Children.Add(new TextBlock
        {
            Text = theme.Description,
            FontSize = 11,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x8B, 0x9B, 0xB4))
        });

        var cardInner = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            Padding = new Thickness(16, 12, 16, 12)
        };
        cardInner.Children.Add(accentPreview);
        cardInner.Children.Add(textStack);

        var card = new Border
        {
            Child = cardInner,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = isActive
                ? new SolidColorBrush(theme.Accent)
                : new SolidColorBrush(Windows.UI.Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF)),
            Background = isActive
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x18, theme.Accent.R, theme.Accent.G, theme.Accent.B))
                : new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x1E, 0x2A, 0x3A)),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        card.Tapped += (_, _) =>
        {
            ThemeManager.SetTheme(theme);
            UpdateSelection();
        };

        card.PointerEntered += (_, _) =>
        {
            if (theme.Name != ThemeManager.Current?.Name)
                card.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, theme.Accent.R, theme.Accent.G, theme.Accent.B));
        };

        card.PointerExited += (_, _) =>
        {
            if (theme.Name != ThemeManager.Current?.Name)
                card.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
        };

        _themeCards.Add(card);
        return card;
    }

    private void UpdateSelection()
    {
        var themes = ThemeManager.AvailableThemes;
        for (int i = 0; i < themes.Length && i < _themeCards.Count; i++)
        {
            var theme = themes[i];
            var card = _themeCards[i];
            var isActive = theme.Name == ThemeManager.Current?.Name;

            card.BorderBrush = isActive
                ? new SolidColorBrush(theme.Accent)
                : new SolidColorBrush(Windows.UI.Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
            card.Background = isActive
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x18, theme.Accent.R, theme.Accent.G, theme.Accent.B))
                : new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x1E, 0x2A, 0x3A));
        }
    }

    private static TextBlock CreateSectionTitle(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x50, 0x8B, 0x9B, 0xB4)),
            CharacterSpacing = 50,
            Margin = new Thickness(0, 8, 0, 0)
        };
    }

    private static Border CreateInfoCard(string title, string description)
    {
        var stack = new StackPanel { Spacing = 4, Padding = new Thickness(16, 12, 16, 12) };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = SteamColors.TextBrush
        });
        stack.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 11,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x80, 0x8B, 0x9B, 0xB4))
        });

        return new Border
        {
            Child = stack,
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF)),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x1E, 0x2A, 0x3A))
        };
    }
}
