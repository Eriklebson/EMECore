using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using EMECore.Core.Models;
using EMECore.WinUI.Theme;
using Windows.Graphics;
using WinRT.Interop;
using System.Runtime.InteropServices;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace EMECore.WinUI.Views;

public sealed class AchievementNotificationWindow
{
    private static readonly List<AchievementNotificationWindow> _instances = new();
    private static readonly object _instancesLock = new();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    private readonly Window _window;
    private readonly Border _container;
    private readonly TextBlock _achievementName;
    private readonly TextBlock _achievementDesc;
    private readonly TextBlock _gameName;
    private readonly FontIcon _icon;
    private readonly DispatcherTimer _hideTimer;
    private readonly DispatcherTimer _progressTimer;
    private readonly Border _progressBar;
    private readonly Storyboard _showAnimation;
    private readonly Storyboard _hideAnimation;

    public AchievementNotificationWindow()
    {
        _window = new Window
        {
            Title = "Achievement Notification",
            SystemBackdrop = new DesktopAcrylicBackdrop()
        };

        // Configure window
        var hwnd = WindowNative.GetWindowHandle(_window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // Set size and position (bottom-right corner)
        var screenWidth = GetSystemMetrics(SM_CXSCREEN);
        var screenHeight = GetSystemMetrics(SM_CYSCREEN);
        var windowWidth = 250;
        var windowHeight = 100;
        var x = screenWidth - windowWidth - 0;
        var y = screenHeight - windowHeight - 0;

        // Make window topmost and frameless
        appWindow.SetPresenter(AppWindowPresenterKind.CompactOverlay);

        // Set size and position
        appWindow.Resize(new SizeInt32 { Width = windowWidth, Height = windowHeight });
        appWindow.Move(new PointInt32 { X = x, Y = y });

        // Configure title bar to be invisible
        var titleBar = appWindow.TitleBar;
        titleBar.ExtendsContentIntoTitleBar = true;
        titleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        titleBar.BackgroundColor = Colors.Transparent;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveForegroundColor = Colors.Transparent;
        titleBar.ForegroundColor = Colors.Transparent;

        // Main container
        _container = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xF5, 0x17, 0x1e, 0x29)),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x30, 0x66, 0xC0, 0xF4)),
            Opacity = 0
        };

        var mainStack = new StackPanel();

        // Header with gradient
        var header = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 0),
                GradientStops =
                {
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0x40, 0x66, 0xC0, 0xF4), Offset = 0 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(0x00, 0x66, 0xC0, 0xF4), Offset = 1 }
                }
            },
            Padding = new Thickness(8, 4, 8, 4),
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF))
        };

        var headerContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        headerContent.Children.Add(new FontIcon
        {
            Glyph = "\uE8FB",
            FontSize = 12,
            Foreground = SteamColors.BlueBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        headerContent.Children.Add(new TextBlock
        {
            Text = "Conquista Desbloqueada!",
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = SteamColors.BlueBrush,
            VerticalAlignment = VerticalAlignment.Center
        });

        header.Child = headerContent;
        mainStack.Children.Add(header);

        // Content area
        var content = new Grid
        {
            Padding = new Thickness(8, 5, 8, 5),
            ColumnSpacing = 8
        };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Achievement icon
        var iconBorder = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x20, 0x66, 0xC0, 0xF4)),
            VerticalAlignment = VerticalAlignment.Center
        };
        _icon = new FontIcon
        {
            Glyph = "\uE8FB",
            FontSize = 18,
            Foreground = SteamColors.BlueBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        iconBorder.Child = _icon;
        content.Children.Add(iconBorder);

        // Text info
        var textStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2
        };

        _achievementName = new TextBlock
        {
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = SteamColors.TextBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };
        textStack.Children.Add(_achievementName);

        _achievementDesc = new TextBlock
        {
            FontSize = 11,
            Foreground = SteamColors.TextSecondaryBrush,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1
        };
        textStack.Children.Add(_achievementDesc);

        _gameName = new TextBlock
        {
            FontSize = 10,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
            Margin = new Thickness(0, 4, 0, 0)
        };
        textStack.Children.Add(_gameName);

        Grid.SetColumn(textStack, 1);
        content.Children.Add(textStack);
        mainStack.Children.Add(content);

        // Progress bar at bottom
        _progressBar = new Border
        {
            Height = 2,
            Background = SteamColors.BlueBrush,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 250
        };
        mainStack.Children.Add(_progressBar);

        _container.Child = mainStack;
        _window.Content = _container;

        // Timer to hide (5 seconds)
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _hideTimer.Tick += (_, _) => Hide();

        // Progress bar timer
        _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        var progressStep = 250.0 / (5000.0 / 50.0);
        _progressTimer.Tick += (_, _) =>
        {
            _progressBar.Width = Math.Max(0, _progressBar.Width - progressStep);
        };

        // Animations
        _showAnimation = new Storyboard();
        var showAnim = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(250)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(showAnim, _container);
        Storyboard.SetTargetProperty(showAnim, "Opacity");
        _showAnimation.Children.Add(showAnim);

        _hideAnimation = new Storyboard();
        var hideAnim = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(250)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(hideAnim, _container);
        Storyboard.SetTargetProperty(hideAnim, "Opacity");
        _hideAnimation.Children.Add(hideAnim);
        _hideAnimation.Completed += (_, _) =>
        {
            _window.Close();
            lock (_instancesLock)
            {
                _instances.Remove(this);
            }
        };

        lock (_instancesLock)
        {
            _instances.Add(this);
        }
    }

    public void Show(Achievement achievement, string gameName = "")
    {
        _achievementName.Text = achievement.Name;
        _achievementDesc.Text = achievement.Description;
        _gameName.Text = string.IsNullOrEmpty(gameName) ? "Stellar Blade" : gameName;
        _icon.Glyph = achievement.Achieved ? "\uE8FB" : "\uE739";

        if (achievement.Achieved)
        {
            _container.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x40, 0x66, 0xC0, 0xF4));
        }
        else
        {
            _container.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF));
        }

        // Reset progress bar
        _progressBar.Width = 250;

        // Play achievement sound (Steam-like)
        _ = PlayAchievementSoundAsync();

        _window.Activate();
        _showAnimation.Begin();
        _hideTimer.Start();
        _progressTimer.Start();
    }

    public void Hide()
    {
        _hideTimer.Stop();
        _progressTimer.Stop();
        _hideAnimation.Begin();
    }

    private static async Task PlayAchievementSoundAsync()
    {
        try
        {
            var sampleRate = 44100;
            var totalDurationMs = 800;
            var totalSamples = sampleRate * totalDurationMs / 1000;
            var buffer = new byte[totalSamples * 2];

            // Steam-like achievement sound: A5 -> E6 -> A6
            var tones = new[] { (freq: 880, start: 0, dur: 150, vol: 0.3), (freq: 1320, start: 80, dur: 400, vol: 0.3), (freq: 1760, start: 200, dur: 600, vol: 0.2) };

            foreach (var (freq, start, dur, vol) in tones)
            {
                var startSample = sampleRate * start / 1000;
                var endSample = Math.Min(startSample + sampleRate * dur / 1000, totalSamples);

                for (int i = startSample; i < endSample; i++)
                {
                    var t = (double)(i - startSample) / sampleRate;
                    var amplitude = vol * Math.Exp(-t * 5);
                    var sample = (short)(amplitude * Math.Sin(2 * Math.PI * freq * t) * short.MaxValue);
                    var existing = (short)(buffer[i * 2] | (buffer[i * 2 + 1] << 8));
                    var mixed = (short)Math.Clamp(existing + sample, short.MinValue, short.MaxValue);
                    buffer[i * 2] = (byte)(mixed & 0xFF);
                    buffer[i * 2 + 1] = (byte)((mixed >> 8) & 0xFF);
                }
            }

            // Save to temp file and play
            var tempFile = Path.Combine(Path.GetTempPath(), "eme_achievement.wav");
            using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
            {
                // WAV header
                fs.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                fs.Write(BitConverter.GetBytes((uint)(36 + buffer.Length)));
                fs.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                fs.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                fs.Write(BitConverter.GetBytes((uint)16));
                fs.Write(BitConverter.GetBytes((ushort)1));
                fs.Write(BitConverter.GetBytes((ushort)1));
                fs.Write(BitConverter.GetBytes((uint)sampleRate));
                fs.Write(BitConverter.GetBytes((uint)(sampleRate * 2)));
                fs.Write(BitConverter.GetBytes((ushort)2));
                fs.Write(BitConverter.GetBytes((ushort)16));
                fs.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                fs.Write(BitConverter.GetBytes((uint)buffer.Length));
                fs.Write(buffer);
            }

            var player = new MediaPlayer();
            player.Source = MediaSource.CreateFromUri(new Uri($"file:///{tempFile.Replace('\\', '/')}"));
            player.Play();

            await Task.Delay(totalDurationMs + 500);
            player.Dispose();

            try { File.Delete(tempFile); } catch { }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Sound error: {ex}");
        }
    }
}
