using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using EMECore.Hardware.Services;
using EMECore.WinUI.Theme;
using System.IO;

namespace EMECore.WinUI.Views;

public class GamepadCalibrationWindow : Window
{
    private GamepadLayout _layout;
    private Canvas _canvas;
    private Image _image;
    private Border? _dragTarget;
    private TextBlock? _dragLabel;
    private string? _dragKey;
    private double _dragOffsetX, _dragOffsetY;
    private readonly Dictionary<Border, (TextBlock Label, string ButtonKey)> _overlays = new();
    private readonly SolidColorBrush _dragHighlight = new(ThemeManager.WithAlpha(ThemeManager.Current.Accent, 120));
    private readonly SolidColorBrush _normalBg = new(ThemeManager.WithAlpha(ThemeManager.Current.Accent, 80));

    public GamepadCalibrationWindow()
    {
        _layout = GamepadLayoutService.Load();
        Title = "Gamepad Calibration Mode";
        Content = BuildUI();
        ThemeManager.ThemeChanged += OnThemeChanged;
        Closed += (_, _) => { ThemeManager.ThemeChanged -= OnThemeChanged; GamepadLayoutService.InvalidateCache(); };

        Activated += (_, _) =>
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 800, Height = 640 });
        };
    }

    private FrameworkElement BuildUI()
    {
        var root = new Grid { Background = new SolidColorBrush(ThemeManager.Current.Background) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Padding = new Thickness(16, 12, 16, 12) };
        header.Children.Add(new TextBlock { Text = "\uE7F4", FontSize = 18, Foreground = new SolidColorBrush(ThemeManager.Current.Accent),
            FontFamily = new FontFamily("Assets/tabler-icons.ttf#tabler-icons"), VerticalAlignment = VerticalAlignment.Center });
        header.Children.Add(new TextBlock { Text = "Gamepad Calibration", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(ThemeManager.Current.TextPrimary), VerticalAlignment = VerticalAlignment.Center });
        header.Children.Add(new TextBlock { Text = "Arraste os pontos sobre os botoes da imagem", FontSize = 12,
            Foreground = new SolidColorBrush(ThemeManager.Current.TextSecondary), VerticalAlignment = VerticalAlignment.Center });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // Canvas with image
        _canvas = new Canvas { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _canvas.Width = _layout.ImageWidth;
        _canvas.Height = _layout.ImageHeight;

        var imgPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Gamepad", "8bitdoUltimate2.png");
        _image = new Image
        {
            Source = new BitmapImage(new Uri(imgPath)),
            Width = _layout.ImageWidth, Height = _layout.ImageHeight,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top
        };
        _canvas.Children.Add(_image);

        // Create draggable overlays from layout
        foreach (var (key, btn) in _layout.Buttons)
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(btn, _layout.ImageWidth, _layout.ImageHeight);
            var diameter = pr * 2;

            var circle = new Border
            {
                Width = diameter, Height = diameter,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(80, 245, 158, 11)),
                BorderBrush = new SolidColorBrush(ThemeManager.Current.Accent),
                BorderThickness = new(2),
                CornerRadius = new CornerRadius(diameter / 2),
                Tag = key,
                Opacity = 0.85
            };
            Canvas.SetLeft(circle, px - pr);
            Canvas.SetTop(circle, py - pr);

            var label = new TextBlock
            {
                Text = key, FontSize = 8, Foreground = new SolidColorBrush(ThemeManager.Current.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            circle.Child = label;
            _canvas.Children.Add(circle);

            circle.PointerPressed += Circle_PointerPressed;
            circle.PointerMoved += Circle_PointerMoved;
            circle.PointerReleased += Circle_PointerReleased;
            circle.PointerCaptureLost += Circle_PointerCaptureLost;

            _overlays[circle] = (label, key);
        }

        Grid.SetRow(_canvas, 1);
        root.Children.Add(_canvas);

        // Footer
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Padding = new Thickness(16, 8, 16, 12),
            HorizontalAlignment = HorizontalAlignment.Center };
        var saveBtn = new Button { Content = "Salvar Layout", Padding = new Thickness(24, 8, 24, 8) };
        saveBtn.Click += SaveBtn_Click;
        footer.Children.Add(saveBtn);

        var resetBtn = new Button { Content = "Resetar", Padding = new Thickness(24, 8, 24, 8),
            Background = new SolidColorBrush(ThemeManager.Current.Card),
            Foreground = new SolidColorBrush(ThemeManager.Current.TextSecondary) };
        resetBtn.Click += ResetBtn_Click;
        footer.Children.Add(resetBtn);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private void Circle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border circle) return;
        _dragTarget = circle;
        _dragLabel = _overlays[circle].Label;
        _dragKey = _overlays[circle].ButtonKey;
        e.Handled = true;
        circle.CapturePointer(e.Pointer);

        var pos = e.GetCurrentPoint(_canvas);
        var left = Canvas.GetLeft(circle);
        var top = Canvas.GetTop(circle);
        _dragOffsetX = pos.Position.X - left;
        _dragOffsetY = pos.Position.Y - top;

        // Visual feedback: raise opacity
        circle.Opacity = 1.0;
        circle.BorderThickness = new(3);
    }

    private void Circle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragTarget == null || sender is not Border circle) return;
        var pos = e.GetCurrentPoint(_canvas);

        var newLeft = pos.Position.X - _dragOffsetX;
        var newTop = pos.Position.Y - _dragOffsetY;

        // Bounds clamping: keep circle within canvas
        newLeft = Math.Clamp(newLeft, -circle.Width * 0.3, _layout.ImageWidth - circle.Width * 0.7);
        newTop = Math.Clamp(newTop, -circle.Height * 0.3, _layout.ImageHeight - circle.Height * 0.7);

        Canvas.SetLeft(circle, newLeft);
        Canvas.SetTop(circle, newTop);
    }

    private void Circle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragTarget == null || sender is not Border circle) return;
        FinalizeDrag(circle);
        _dragTarget.ReleasePointerCapture(e.Pointer);
        _dragTarget = null;
        _dragLabel = null;
        _dragKey = null;
    }

    private void Circle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border circle) return;
        FinalizeDrag(circle);
        _dragTarget = null;
        _dragLabel = null;
        _dragKey = null;
    }

    private void FinalizeDrag(Border circle)
    {
        // Visual feedback: restore normal appearance
        circle.Opacity = 0.85;
        circle.BorderThickness = new(2);

        // Update label with final position
        var left = Canvas.GetLeft(circle);
        var top = Canvas.GetTop(circle);
        var cx = left + circle.Width / 2;
        var cy = top + circle.Height / 2;
        _overlays[circle].Label.Text = $"{_overlays[circle].ButtonKey}\n({cx:F0},{cy:F0})";
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        foreach (var (circle, (_, key)) in _overlays)
        {
            var left = Canvas.GetLeft(circle);
            var top = Canvas.GetTop(circle);
            var cx = left + circle.Width / 2;
            var cy = top + circle.Height / 2;
            var radius = circle.Width / 2;

            if (_layout.Buttons.ContainsKey(key))
            {
                _layout.Buttons[key].X = Math.Round(cx / _layout.ImageWidth, 3);
                _layout.Buttons[key].Y = Math.Round(cy / _layout.ImageHeight, 3);
                _layout.Buttons[key].Radius = Math.Round(radius / Math.Min(_layout.ImageWidth, _layout.ImageHeight), 3);
            }
        }

        GamepadLayoutService.Save(_layout);
    }

    private void ResetBtn_Click(object sender, RoutedEventArgs e)
    {
        GamepadLayoutService.InvalidateCache();
        _layout = GamepadLayoutService.Load();

        foreach (var (circle, (_, key)) in _overlays)
        {
            if (_layout.Buttons.TryGetValue(key, out var btn))
            {
                var (px, py, pr) = GamepadLayoutService.ToPixels(btn, _layout.ImageWidth, _layout.ImageHeight);
                Canvas.SetLeft(circle, px - pr);
                Canvas.SetTop(circle, py - pr);
                circle.Width = pr * 2;
                circle.Height = pr * 2;
                circle.CornerRadius = new CornerRadius(pr);
                _overlays[circle].Label.Text = key;
            }
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
            ThemeVisualTree.Refresh(Content as DependencyObject, ThemeManager.Previous, ThemeManager.Current));
    }

}
