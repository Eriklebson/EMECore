using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.Win32;
using Windows.UI;
using EMECore.Core.Services;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class SettingsPage : UserControl
{
    private readonly Grid _root;
    private readonly StackPanel _themeList;
    private Border? _selectedCard;

    public event EventHandler? ThemeChanged;

    public SettingsPage()
    {
        _root = new Grid
        {
            Background = new SolidColorBrush(SteamColors.Dark),
            Padding = new Thickness(24)
        };

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var stack = new StackPanel { Spacing = 24 };

        // Header
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock
        {
            Text = "Configurações",
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = SteamColors.TextBrush
        });
        header.Children.Add(new TextBlock
        {
            Text = "Personalize a aparência do EME Core",
            FontSize = 13,
            Foreground = SteamColors.TextSecondaryBrush
        });
        stack.Children.Add(header);

        // Themes section
        var themeSection = new StackPanel { Spacing = 12 };
        themeSection.Children.Add(new TextBlock
        {
            Text = "TEMA",
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = SteamColors.TextSecondaryBrush,
            CharacterSpacing = 180,
            Margin = new Thickness(0, 8, 0, 0)
        });

        _themeList = new StackPanel { Spacing = 8 };
        BuildThemeCards();
        themeSection.Children.Add(_themeList);
        stack.Children.Add(themeSection);

        // Startup toggle
        var startupSection = new StackPanel { Spacing = 12 };
        startupSection.Children.Add(new TextBlock
        {
            Text = "INICIALIZAÇÃO",
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = SteamColors.TextSecondaryBrush,
            CharacterSpacing = 180,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var startupCard = CreateCard();
        var startupRow = new Grid { Padding = new Thickness(16) };
        startupRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        startupRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var startupText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        startupText.Children.Add(new TextBlock
        {
            Text = "Iniciar com o Windows",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            Foreground = SteamColors.TextBrush
        });
        startupText.Children.Add(new TextBlock
        {
            Text = "Abrir o EME Core automaticamente ao ligar o computador",
            FontSize = 12,
            Foreground = SteamColors.TextSecondaryBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(startupText, 0);
        startupRow.Children.Add(startupText);

        var startupToggle = new ToggleSwitch
        {
            IsOn = SettingsService.Get("auto_start", "false") == "true",
            OnContent = "Sim",
            OffContent = "Não",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0)
        };
        startupToggle.Toggled += (_, e) =>
        {
            SettingsService.Set("auto_start", startupToggle.IsOn ? "true" : "false");
            SetStartupRegistry(startupToggle.IsOn);
        };
        Grid.SetColumn(startupToggle, 1);
        startupRow.Children.Add(startupToggle);

        startupCard.Child = startupRow;
        startupSection.Children.Add(startupCard);
        stack.Children.Add(startupSection);

        // FPS Overlay section
        var overlaySection = new StackPanel { Spacing = 12 };
        overlaySection.Children.Add(new TextBlock
        {
            Text = "OVERLAY DE FPS",
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = SteamColors.TextSecondaryBrush,
            CharacterSpacing = 180,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var overlayCard = CreateCard();
        var overlayStack = new StackPanel { Padding = new Thickness(16), Spacing = 12 };

        overlayStack.Children.Add(new TextBlock
        {
            Text = "Estatísticas detalhadas",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            Foreground = SteamColors.TextBrush
        });

        var toggles = new StackPanel { Spacing = 8 };
        toggles.Children.Add(CreateToggleOption("1% Low", "overlay_show_low1", "Mostra o frame time do percentil mais lento"));
        toggles.Children.Add(CreateToggleOption("0.1% Low", "overlay_show_low01", "Mostra o frame time do percentil extremo"));
        toggles.Children.Add(CreateToggleOption("Frame Time", "overlay_show_frametime", "Mostra o tempo de cada frame em ms"));
        overlayStack.Children.Add(toggles);

        overlayCard.Child = overlayStack;
        overlaySection.Children.Add(overlayCard);
        stack.Children.Add(overlaySection);

        // Info section
        var infoSection = new StackPanel { Spacing = 12 };
        infoSection.Children.Add(new TextBlock
        {
            Text = "SOBRE",
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = SteamColors.TextSecondaryBrush,
            CharacterSpacing = 180,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var infoCard = CreateCard();
        var infoStack = new StackPanel { Padding = new Thickness(16), Spacing = 8 };
        infoStack.Children.Add(CreateInfoRow("Versão", "2.18.0.0"));
        infoStack.Children.Add(CreateInfoRow("Desenvolvedor", "Eriklebson Martins de Paiva"));
        infoStack.Children.Add(CreateInfoRow("Plataforma", "Windows 10/11 x64"));
        infoCard.Child = infoStack;
        infoSection.Children.Add(infoCard);
        stack.Children.Add(infoSection);

        scroll.Content = stack;
        _root.Children.Add(scroll);
        Content = _root;
    }

    private void BuildThemeCards()
    {
        _themeList.Children.Clear();
        var currentName = SettingsService.Get("theme", "Padrão");

        foreach (var theme in ThemeManager.AvailableThemes)
        {
            var card = CreateCard();
            card.Tag = theme.Name;
            card.Margin = new Thickness(0, 2, 0, 2);
            card.Padding = new Thickness(0);

            if (theme.Name == currentName)
            {
                card.BorderBrush = new SolidColorBrush(theme.Accent);
                card.BorderThickness = new Thickness(2);
                _selectedCard = card;
            }

            var row = new Grid { Padding = new Thickness(16, 12, 16, 12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Color preview dots
            var dots = new StackPanel
            {
                Tag = ThemeVisualTree.PreviewTag,
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };
            dots.Children.Add(CreateColorDot(theme.Accent));
            dots.Children.Add(CreateColorDot(theme.Background));
            dots.Children.Add(CreateColorDot(theme.Card));
            dots.Children.Add(CreateColorDot(theme.TextPrimary));
            Grid.SetColumn(dots, 0);
            row.Children.Add(dots);

            // Theme name + description
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = theme.Name,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = SteamColors.TextBrush
            });
            info.Children.Add(new TextBlock
            {
                Text = theme.Description,
                FontSize = 12,
                Foreground = SteamColors.TextSecondaryBrush,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(info, 1);
            row.Children.Add(info);

            // Selection indicator
            var indicator = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(SteamColors.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            if (theme.Name == currentName)
            {
                indicator.Background = new SolidColorBrush(theme.Accent);
                indicator.BorderBrush = new SolidColorBrush(theme.Accent);
            }
            Grid.SetColumn(indicator, 2);
            row.Children.Add(indicator);

            card.Child = row;

            card.PointerEntered += (_, _) =>
            {
                if (card != _selectedCard)
                    card.Background = new SolidColorBrush(SteamColors.CardHover);
            };
            card.PointerExited += (_, _) =>
            {
                if (card != _selectedCard)
                    card.Background = new SolidColorBrush(SteamColors.Card);
            };

            card.Tapped += (_, _) => SelectTheme(theme, card, indicator);

            _themeList.Children.Add(card);
        }
    }

    private void SelectTheme(AppTheme theme, Border card, Border indicator)
    {
        ThemeChangeDiagnostics.StartSession(theme.Name);
        TraceThemeChange($"início seleção: {theme.Name}");
        // Reset previous selection
        if (_selectedCard != null)
        {
            _selectedCard.BorderBrush = new SolidColorBrush(ThemeManager.Current.Border);
            _selectedCard.BorderThickness = new Thickness(1);
            _selectedCard.Background = new SolidColorBrush(ThemeManager.Current.Card);
        }

        // Apply new selection
        card.BorderBrush = new SolidColorBrush(theme.Accent);
        card.BorderThickness = new Thickness(2);
        _selectedCard = card;

        indicator.Background = new SolidColorBrush(theme.Accent);
        indicator.BorderBrush = new SolidColorBrush(theme.Accent);

        // Reset all indicators
        foreach (var child in _themeList.Children)
        {
            if (child is Border b && b.Child is Grid g && g.Children.Count > 2 && g.Children[2] is Border ind && b != card)
            {
                ind.Background = new SolidColorBrush(Colors.Transparent);
                ind.BorderBrush = new SolidColorBrush(theme.TextSecondary);
            }
        }

        // Save and apply
        SettingsService.Set("theme", theme.Name);
        TraceThemeChange("preferência salva");
        ThemeManager.SetTheme(theme);
        TraceThemeChange("ThemeManager concluído");
        ThemeChanged?.Invoke(this, EventArgs.Empty);
        TraceThemeChange("evento global concluído");
    }

    private static void TraceThemeChange(string stage)
    {
        ThemeChangeDiagnostics.Write(stage);
    }

    private static Border CreateCard() => new()
    {
        Background = new SolidColorBrush(ThemeManager.Current.Card),
        CornerRadius = new CornerRadius(8),
        BorderThickness = new Thickness(1),
        BorderBrush = new SolidColorBrush(ThemeManager.Current.Border),
        Padding = new Thickness(16)
    };

    private static Border CreateColorDot(Color color) => new()
    {
        Width = 16,
        Height = 16,
        CornerRadius = new CornerRadius(8),
        Background = new SolidColorBrush(color),
        BorderThickness = new Thickness(1),
        BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
    };

    private StackPanel CreateToggleOption(string label, string key, string description)
    {
        var row = new Grid { VerticalAlignment = VerticalAlignment.Center };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            Foreground = SteamColors.TextBrush
        });
        text.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 11,
            Foreground = SteamColors.TextSecondaryBrush,
            Margin = new Thickness(0, 1, 0, 0)
        });
        Grid.SetColumn(text, 0);
        row.Children.Add(text);

        var toggle = new ToggleSwitch
        {
            IsOn = SettingsService.Get(key, "1") == "1",
            OnContent = "On",
            OffContent = "Off",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0)
        };
        toggle.Toggled += (_, _) =>
        {
            SettingsService.Set(key, toggle.IsOn ? "1" : "0");
        };
        Grid.SetColumn(toggle, 1);
        row.Children.Add(toggle);

        return new StackPanel { Children = { row } };
    }

    private static Grid CreateInfoRow(string label, string value)
    {
        var row = new Grid { VerticalAlignment = VerticalAlignment.Center };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 13,
            Foreground = SteamColors.TextSecondaryBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        row.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 13,
            Foreground = SteamColors.TextBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn((FrameworkElement)row.Children[1], 1);

        return row;
    }

    private static void SetStartupRegistry(bool enabled)
    {
        try
        {
            const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            const string valueName = "EMECore";

            using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? "";
                key.SetValue(valueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(valueName, false);
            }
        }
        catch { }
    }
}
