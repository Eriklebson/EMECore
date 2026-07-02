using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using EMECore.Core.Models;
using EMECore.Hardware.Services;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class MonitorWindow : Window
{
    private readonly HardwareMonitorService _monitor;
    private readonly DispatcherTimer _timer;
    private readonly TextBlock _cpuInfo, _gpuInfo, _ramInfo;
    private readonly Border _cpuBar, _gpuBar, _ramBar;
    private readonly StackPanel _fansPanel, _tempsPanel;

    public MonitorWindow()
    {
        Title = "Monitor de Hardware";
        _monitor = new HardwareMonitorService();

        var root = new Grid { Background = new SolidColorBrush(SteamColors.Darkest) };
        var scroll = new ScrollViewer { Padding = new Thickness(12, 40, 12, 12) };
        var content = new StackPanel { Spacing = 10 };

        var topRow = new Grid { ColumnSpacing = 10 };
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        (_cpuInfo, _cpuBar) = AddCard(content, topRow, "CPU", 0);
        (_gpuInfo, _gpuBar) = AddCard(content, topRow, "GPU", 1);
        (_ramInfo, _ramBar) = AddCard(content, null, "RAM", -1);

        content.Children.Add(topRow);

        _tempsPanel = new StackPanel { Spacing = 4 };
        content.Children.Add(WrapCard("Temperaturas", _tempsPanel));

        _fansPanel = new StackPanel { Spacing = 4 };
        content.Children.Add(WrapCard("Ventoinhas", _fansPanel));

        scroll.Content = content;
        root.Children.Add(scroll);
        Content = root;

        Closed += (_, _) => { _timer.Stop(); _monitor.Dispose(); };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Refresh();
    }

    private static (TextBlock, Border) AddCard(StackPanel parent, Grid? topRow, string title, int col)
    {
        var stack = new StackPanel { Spacing = 6 };
        var hdr = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        hdr.Children.Add(new FontIcon { Glyph = "\uE950", FontSize = 14, Foreground = SteamColors.BlueBrush });
        hdr.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SteamColors.TextBrush });
        stack.Children.Add(hdr);
        var info = new TextBlock { FontSize = 11, Foreground = SteamColors.TextSecondaryBrush };
        stack.Children.Add(info);
        var bg = new Border { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0x66, 0xc0, 0xf4)), CornerRadius = new CornerRadius(3), Height = 8 };
        var fill = new Border { Background = SteamColors.BlueBrush, CornerRadius = new CornerRadius(3), Height = 8, HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
        var g = new Grid(); g.Children.Add(bg); g.Children.Add(fill);
        stack.Children.Add(g);
        var card = new Border { Background = SteamColors.CardBrush, CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Child = stack };

        if (topRow != null && col >= 0)
        {
            Grid.SetColumn(card, col);
            topRow.Children.Add(card);
        }
        else parent.Children.Add(card);

        return (info, fill);
    }

    private static Border WrapCard(string title, StackPanel inner)
    {
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SteamColors.TextBrush });
        stack.Children.Add(inner);
        return new Border { Background = SteamColors.CardBrush, CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Child = stack };
    }

    private void Refresh()
    {
        try
        {
            var s = _monitor.Collect();
            UpdateCard(_cpuInfo, _cpuBar, s.CpuUsage, $"{s.CpuModel} | {s.CpuUsage:F0}% | {s.CpuTemp:F0}C | {s.CpuCores} cores");
            UpdateCard(_gpuInfo, _gpuBar, s.GpuUsage, $"{s.GpuModel} | {s.GpuUsage:F0}% | {s.GpuTemp:F0}C");
            UpdateCard(_ramInfo, _ramBar, s.TotalRam > 0 ? s.UsedRam / s.TotalRam * 100 : 0, $"{s.UsedRam:F1} / {s.TotalRam:F1} GB | {s.RamSpeed} MHz");

            _tempsPanel.Children.Clear();
            if (s.CpuTemp > 0) _tempsPanel.Children.Add(new TextBlock { Text = $"CPU: {s.CpuTemp:F0}°C", FontSize = 11, Foreground = TempColor(s.CpuTemp) });
            if (s.CpuPackageTemp > 0) _tempsPanel.Children.Add(new TextBlock { Text = $"CPU Package: {s.CpuPackageTemp:F0}°C", FontSize = 11, Foreground = TempColor(s.CpuPackageTemp) });
            if (s.GpuTemp > 0) _tempsPanel.Children.Add(new TextBlock { Text = $"GPU: {s.GpuTemp:F0}°C", FontSize = 11, Foreground = TempColor(s.GpuTemp) });

            _fansPanel.Children.Clear();
            if (s.Fans.Count == 0) _fansPanel.Children.Add(new TextBlock { Text = "Nenhuma ventoinha detectada", FontSize = 11, Foreground = SteamColors.TextSecondaryBrush });
            foreach (var f in s.Fans)
                _fansPanel.Children.Add(new TextBlock { Text = $"{f.Name}: {f.Rpm:F0} RPM ({f.DutyPercent:F0}%)", FontSize = 11, Foreground = SteamColors.TextSecondaryBrush });
        }
        catch { }
    }

    private static void UpdateCard(TextBlock info, Border bar, double pct, string text)
    {
        info.Text = text;
        bar.Width = Math.Max(0, Math.Min(200, pct * 2));
        bar.Background = pct >= 85 ? SteamColors.RedBrush : pct >= 60 ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF0, 0xA5, 0x00)) : SteamColors.BlueBrush;
    }

    private static SolidColorBrush TempColor(double t) => new(t >= 85 ? Colors.Red : t >= 70 ? Windows.UI.Color.FromArgb(0xFF, 0xF0, 0xA5, 0x00) : t >= 50 ? Colors.LimeGreen : Colors.CornflowerBlue);
}
