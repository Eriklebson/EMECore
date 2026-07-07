using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EMECore.Hardware.Services;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed class FishingMacroWindow : Window
{
    private readonly FishingMacroService _macroService;
    private readonly TextBlock _statusText;
    private readonly TextBlock _fishCountText;
    private readonly Button _startButton;
    private readonly Button _stopButton;
    private int _fishCaught;

    public FishingMacroWindow()
    {
        Title = "Macro de Pesca - Stellar Blade";
        _macroService = new FishingMacroService();

        // Set window size
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 400, Height = 300 });

        var root = new Grid { Background = new SolidColorBrush(SteamColors.Darkest) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header
        var header = new Border
        {
            Background = new SolidColorBrush(SteamColors.Darker),
            Padding = new Thickness(16, 12, 16, 12)
        };
        var headerContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        headerContent.Children.Add(new FontIcon
        {
            Glyph = "\uE8FB",
            FontSize = 24,
            Foreground = SteamColors.BlueBrush
        });
        headerContent.Children.Add(new TextBlock
        {
            Text = "Macro de Pesca Automática",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = SteamColors.TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Child = headerContent;
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // Main content
        var mainContent = new StackPanel
        {
            Spacing = 20,
            Padding = new Thickness(24),
            VerticalAlignment = VerticalAlignment.Center
        };

        // Status card
        var statusCard = new Border
        {
            Background = SteamColors.CardBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16)
        };
        var statusStack = new StackPanel { Spacing = 8 };
        statusStack.Children.Add(new TextBlock
        {
            Text = "Status",
            FontSize = 12,
            Foreground = SteamColors.TextSecondaryBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        _statusText = new TextBlock
        {
            Text = "Pronto - Pressione Ctrl+E para iniciar",
            FontSize = 14,
            Foreground = SteamColors.TextBrush,
            TextWrapping = TextWrapping.Wrap
        };
        statusStack.Children.Add(_statusText);
        statusCard.Child = statusStack;
        mainContent.Children.Add(statusCard);

        // Fish count card
        var fishCard = new Border
        {
            Background = SteamColors.CardBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16)
        };
        var fishStack = new StackPanel { Spacing = 8 };
        fishStack.Children.Add(new TextBlock
        {
            Text = "Peixes Pescados",
            FontSize = 12,
            Foreground = SteamColors.TextSecondaryBrush,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        _fishCountText = new TextBlock
        {
            Text = "0",
            FontSize = 32,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = SteamColors.BlueBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        fishStack.Children.Add(_fishCountText);
        fishCard.Child = fishStack;
        mainContent.Children.Add(fishCard);

        // Hotkey info
        var hotkeyInfo = new TextBlock
        {
            Text = "Ctrl+E: Iniciar/Parar | Ctrl+Shift+F: Marcar mordida (modo aprender)",
            FontSize = 11,
            Foreground = SteamColors.TextSecondaryBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = Microsoft.UI.Xaml.TextAlignment.Center
        };
        mainContent.Children.Add(hotkeyInfo);

        Grid.SetRow(mainContent, 1);
        root.Children.Add(mainContent);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(16)
        };

        _startButton = new Button
        {
            Content = CreateButtonContent("\uE768", "Iniciar Pesca"),
            Background = SteamColors.GreenBrush,
            Foreground = new SolidColorBrush(Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(24, 12, 24, 12),
            MinWidth = 150
        };
        _startButton.Click += StartButton_Click;
        buttonPanel.Children.Add(_startButton);

        _stopButton = new Button
        {
            Content = CreateButtonContent("\uE71A", "Parar"),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0xFF, 0x44, 0x44)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x66, 0x66)),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(24, 12, 24, 12),
            MinWidth = 150,
            IsEnabled = false
        };
        _stopButton.Click += StopButton_Click;
        buttonPanel.Children.Add(_stopButton);

        var learnButton = new Button
        {
            Content = CreateButtonContent("\uE8FB", "Aprender"),
            Background = SteamColors.BlueBrush,
            Foreground = new SolidColorBrush(Colors.White),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(24, 12, 24, 12),
            MinWidth = 150
        };
        learnButton.Click += LearnButton_Click;
        buttonPanel.Children.Add(learnButton);

        Grid.SetRow(buttonPanel, 2);
        root.Children.Add(buttonPanel);

        Content = root;

        // Subscribe to events
        _macroService.StatusChanged += MacroService_StatusChanged;
        _macroService.FishCaught += MacroService_FishCaught;
        _macroService.ToggleRequested += (_, _) => DispatcherQueue.TryEnqueue(ToggleMacro);

        // Install keyboard hook
        _macroService.InstallHook();

        Closed += (_, _) =>
        {
            _macroService.Dispose();
        };
    }

    private static StackPanel CreateButtonContent(string glyph, string text)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                new FontIcon { Glyph = glyph, FontSize = 14 },
                new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }
            }
        };
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        _macroService.StartFishing();
        _startButton.IsEnabled = false;
        _stopButton.IsEnabled = true;
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _macroService.StopFishing();
        _startButton.IsEnabled = true;
        _stopButton.IsEnabled = false;
    }

    private void LearnButton_Click(object sender, RoutedEventArgs e)
    {
        if (_macroService.IsLearning)
        {
            _macroService.StopLearning();
        }
        else
        {
            _macroService.StartLearning();
        }
    }

    private void ToggleMacro()
    {
        if (_macroService.IsRunning)
        {
            StopButton_Click(this!, new RoutedEventArgs());
        }
        else
        {
            StartButton_Click(this!, new RoutedEventArgs());
        }
    }

    private void MacroService_StatusChanged(object? sender, string status)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _statusText.Text = status;
        });
    }

    private void MacroService_FishCaught(object? sender, int count)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _fishCaught += count;
            _fishCountText.Text = _fishCaught.ToString();
        });
    }
}
