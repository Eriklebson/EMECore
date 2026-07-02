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
    private readonly TextBlock _cpuPct, _cpuCoreTemp, _cpuPkgTemp, _cpuModel,
        _gpuPct, _gpuCoreTemp, _gpuHotTemp, _gpuModel,
        _ramPct, _ramInfo;
    private readonly Grid _cpuBar, _gpuBar, _ramBar;
    private readonly StackPanel _fansPanel;

    public MonitorWindow()
    {
        Title = "Monitor de Hardware";
        _monitor = new HardwareMonitorService();

        var root = new Grid { Background = new SolidColorBrush(SteamColors.Darkest) };
        var scroll = new ScrollViewer { Padding = new Thickness(12, 40, 12, 12) };
        var content = new StackPanel { Spacing = 10 };

        var row1 = new Grid { ColumnSpacing = 10 };
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        (_cpuPct, _cpuCoreTemp, _cpuPkgTemp, _cpuBar, _cpuModel) = BuildCard(row1, "CPU", 0, true);
        (_gpuPct, _gpuCoreTemp, _gpuHotTemp, _gpuBar, _gpuModel) = BuildCard(row1, "GPU", 1, true);
        content.Children.Add(row1);

        var row2 = new Grid { ColumnSpacing = 10 };
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var r = BuildCard(row2, "RAM", 0, false);
        _ramPct = r.pct; _ramBar = r.bar; _ramInfo = r.model;

        var fc = new Border { Background = SteamColors.CardBrush, CornerRadius = new CornerRadius(8), Padding = new Thickness(14) };
        var fs = new StackPanel { Spacing = 4 };
        fs.Children.Add(new TextBlock { Text = "Ventoinhas", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SteamColors.TextBrush });
        _fansPanel = new StackPanel { Spacing = 2 };
        fs.Children.Add(_fansPanel); fc.Child = fs;
        Grid.SetColumn(fc, 1); row2.Children.Add(fc);
        content.Children.Add(row2);

        scroll.Content = content; root.Children.Add(scroll); Content = root;

        Closed += (_, _) => { _timer.Stop(); _monitor.Dispose(); };
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh(); _timer.Start(); Refresh();
    }

    private static (TextBlock pct, TextBlock t1, TextBlock t2, Grid bar, TextBlock model) BuildCard(Grid parent, string title, int col, bool hasTemps)
    {
        var s = new StackPanel { Spacing = 6 };

        var hdr = new Grid();
        hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        hdr.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SteamColors.TextBrush, VerticalAlignment = VerticalAlignment.Center });

        TextBlock t1 = null!, t2 = null!;
        if (hasTemps)
        {
            var ts = new StackPanel { Spacing = 1, HorizontalAlignment = HorizontalAlignment.Right };
            t1 = new TextBlock { FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = SteamColors.GreenBrush, HorizontalAlignment = HorizontalAlignment.Right };
            t2 = new TextBlock { FontSize = 12, Foreground = SteamColors.TextSecondaryBrush, HorizontalAlignment = HorizontalAlignment.Right };
            ts.Children.Add(t1); ts.Children.Add(t2);
            Grid.SetColumn(ts, 1); hdr.Children.Add(ts);
        }
        s.Children.Add(hdr);

        var model = new TextBlock { FontSize = 11, Foreground = SteamColors.TextSecondaryBrush, TextTrimming = TextTrimming.CharacterEllipsis };
        s.Children.Add(model);

        var pct = new TextBlock { FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = SteamColors.TextBrush };
        s.Children.Add(pct);

        var fill = new Border { Background = SteamColors.GreenBrush, CornerRadius = new CornerRadius(3), Height = 6 };
        var bg = new Border { Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0x66, 0xc0, 0xf4)), CornerRadius = new CornerRadius(3), Height = 6 };
        var bar = new Grid();
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100, GridUnitType.Star) });
        Grid.SetColumn(fill, 0); Grid.SetColumn(bg, 1); bar.Children.Add(fill); bar.Children.Add(bg);
        s.Children.Add(bar);

        var card = new Border { Background = SteamColors.CardBrush, CornerRadius = new CornerRadius(8), Padding = new Thickness(14), Child = s };
        Grid.SetColumn(card, col); parent.Children.Add(card);
        return (pct, t1, t2, bar, model);
    }

    private void Refresh()
    {
        try
        {
            var s = _monitor.Collect();
            _cpuModel.Text = s.CpuModel;
            _cpuPct.Text = $"{s.CpuUsage:F0}%"; _cpuPct.Foreground = C(s.CpuUsage);
            SetBar(_cpuBar, s.CpuUsage, C(s.CpuUsage));
            _cpuCoreTemp.Text = s.CpuTemp > 0 ? $"{s.CpuTemp:F0}°C" : ""; _cpuCoreTemp.Foreground = T(s.CpuTemp);
            _cpuPkgTemp.Text = s.CpuPackageTemp > 0 ? $"Pkg {s.CpuPackageTemp:F0}°C" : ""; _cpuPkgTemp.Foreground = T(s.CpuPackageTemp);

            _gpuModel.Text = s.GpuModel;
            _gpuPct.Text = $"{s.GpuUsage:F0}%"; _gpuPct.Foreground = C(s.GpuUsage);
            SetBar(_gpuBar, s.GpuUsage, C(s.GpuUsage));
            _gpuCoreTemp.Text = s.GpuTemp > 0 ? $"{s.GpuTemp:F0}°C" : ""; _gpuCoreTemp.Foreground = T(s.GpuTemp);
            _gpuHotTemp.Text = "";

            var rp = s.TotalRam > 0 ? s.UsedRam / s.TotalRam * 100 : 0;
            _ramInfo.Text = $"{s.UsedRam:F1} / {s.TotalRam:F1} GB  |  {s.RamSpeed} MHz";
            _ramPct.Text = $"{rp:F0}%"; _ramPct.Foreground = C(rp);
            SetBar(_ramBar, rp, C(rp));

            _fansPanel.Children.Clear();
            if (s.Fans.Count == 0) _fansPanel.Children.Add(new TextBlock { Text = "Nenhuma ventoinha", FontSize = 11, Foreground = SteamColors.TextSecondaryBrush });
            else foreach (var f in s.Fans) _fansPanel.Children.Add(new TextBlock { Text = $"{f.Name}: {f.Rpm:F0} RPM ({f.DutyPercent:F0}%)", FontSize = 11, Foreground = SteamColors.TextSecondaryBrush });
        }
        catch { }
    }

    private static void SetBar(Grid bar, double pct, SolidColorBrush color)
    {
        pct = Math.Max(0, Math.Min(100, pct));
        bar.ColumnDefinitions[0].Width = new GridLength(pct, GridUnitType.Star);
        bar.ColumnDefinitions[1].Width = new GridLength(100 - pct, GridUnitType.Star);
        ((Border)bar.Children[0]).Background = color;
    }

    private static SolidColorBrush C(double p) => p >= 85 ? SteamColors.RedBrush : p >= 60 ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF0, 0xA5, 0x00)) : SteamColors.GreenBrush;
    private static SolidColorBrush T(double t) => t >= 85 ? SteamColors.RedBrush : t >= 70 ? new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xF0, 0xA5, 0x00)) : t >= 50 ? SteamColors.GreenBrush : SteamColors.BlueBrush;
}
