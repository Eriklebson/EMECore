using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Numerics;
using EMECore.Core.Models;
using EMECore.Hardware.Services;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI.Views;

public sealed partial class MonitorWindow : Window
{
    private readonly HardwareMonitorService _monitor;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _graphTimer;

    // Motherboard
    private readonly TextBlock _mbModel, _mbTemp, _mbVrmTemp, _mbVoltage;
    private readonly StackPanel _mbFansPanel;

    // RAM
    private readonly TextBlock _ramPct, _ramInfo, _ramModel, _ramVoltage;
    private readonly Grid _ramBar;

    // CPU
    private readonly TextBlock _cpuPct, _cpuCoreTemp, _cpuPkgTemp, _cpuModel, _cpuVoltage;
    private readonly Grid _cpuBar;
    private readonly Canvas _cpuGraph;
    private readonly List<double> _cpuHistory = new();
    private readonly StackPanel _cpuFansPanel;

    // GPU
    private readonly TextBlock _gpuPct, _gpuCoreTemp, _gpuModel, _gpuVoltage;
    private readonly Grid _gpuBar;
    private readonly Canvas _gpuGraph;
    private readonly List<double> _gpuHistory = new();
    private readonly StackPanel _gpuFansPanel;

    // Disk
    private readonly TextBlock _diskName, _diskPct, _diskInfo, _diskRead, _diskWrite;
    private readonly Grid _diskBar;

    // Network
    private readonly TextBlock _netName, _netDown, _netUp;
    private readonly Canvas _netDownGraph, _netUpGraph;
    private readonly List<double> _netDownHistory = new();
    private readonly List<double> _netUpHistory = new();
    private double _lastNetDown, _lastNetUp;

    // FPS
    private readonly TextBlock _fpsValue, _fpsInfo, _fpsLabel;
    private readonly Border _fpsCard;

    private int _fanCount;
    private bool _isMoving;

    // Cores por componente
    private static readonly SolidColorBrush MbColor = new(ColorFromHex("#F472B6"));
    private static readonly SolidColorBrush RamColor = new(ColorFromHex("#C084FC"));
    private static readonly SolidColorBrush CpuColor = new(ColorFromHex("#4ADE80"));
    private static readonly SolidColorBrush GpuColor = new(ColorFromHex("#60A5FA"));
    private static readonly SolidColorBrush DiskColor = new(ColorFromHex("#FBBF24"));
    private static readonly SolidColorBrush NetColor = new(ColorFromHex("#34D399"));
    private static readonly SolidColorBrush FpsColor = new(ColorFromHex("#FB923C"));
    private static readonly SolidColorBrush CardBg = new(ColorFromHex("#111827"));
    private static readonly SolidColorBrush CardBorder = new(ColorFromHex("#1F2937"));
    private static readonly SolidColorBrush SurfaceBg = new(ColorFromHex("#0F172A"));
    private static readonly SolidColorBrush SubtleText = new(ColorFromHex("#64748B"));

    public MonitorWindow()
    {
        Title = "Hardware Monitor";
        _monitor = new HardwareMonitorService();

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 960, Height = 640 });

        var root = new Grid { Background = SurfaceBg };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ===== SIDEBAR =====
        var sidebar = new Border
        {
            Background = new SolidColorBrush(ColorFromHex("#0D1320")),
            BorderThickness = new Thickness(0, 0, 1, 0), BorderBrush = CardBorder,
            Padding = new Thickness(0, 48, 0, 16)
        };
        var sidebarStack = new StackPanel { Spacing = 4, Padding = new Thickness(12) };
        var logoStack = new StackPanel { Spacing = 4, Margin = new Thickness(8, 0, 8, 24) };
        logoStack.Children.Add(new TextBlock { Text = "E.M.E Core", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) });
        logoStack.Children.Add(new TextBlock { Text = "Hardware Monitor", FontSize = 12, Foreground = SubtleText });
        sidebarStack.Children.Add(logoStack);
        sidebarStack.Children.Add(CreateNavItem("\uE9F5", "Hardware", true, CpuColor));
        sidebarStack.Children.Add(CreateNavItem("\uE7F4", "Monitores", false, SubtleText));
        sidebarStack.Children.Add(CreateNavItem("\uE771", "Periféricos", false, SubtleText));
        sidebar.Child = sidebarStack;
        Grid.SetColumn(sidebar, 0);
        root.Children.Add(sidebar);

        // ===== MAIN CONTENT =====
        var mainContent = new ScrollViewer { Padding = new Thickness(24, 48, 24, 24), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var contentStack = new StackPanel { Spacing = 16 };

        var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new TextBlock { Text = "Hardware", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) });
        header.Children.Add(new TextBlock { Text = "Monitoramento em tempo real do seu sistema", FontSize = 13, Foreground = SubtleText });
        contentStack.Children.Add(header);

        // ===== ROW 1: Motherboard + RAM =====
        var row1 = new Grid { ColumnSpacing = 16 };
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Motherboard Card
        var mbCard = CreateCard();
        var mbStack = new StackPanel { Spacing = 12 };
        var mbHeader = CreateCardHeaderGrid("\uE950", "PLACA MÃE", MbColor);
        _mbVoltage = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(ColorFromHex("#9CA3AF")), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_mbVoltage, 1);
        ((Grid)mbHeader).Children.Add(_mbVoltage);
        mbStack.Children.Add(mbHeader);
        _mbModel = new TextBlock { FontSize = 11, Foreground = SubtleText, TextTrimming = TextTrimming.CharacterEllipsis };
        mbStack.Children.Add(_mbModel);

        var mbMetricsGrid = new Grid { ColumnSpacing = 32 };
        mbMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        mbMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var mbTempStack = new StackPanel { Spacing = 2 };
        mbTempStack.Children.Add(new TextBlock { Text = "TEMPERATURA", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _mbTemp = new TextBlock { FontSize = 32, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = MbColor };
        mbTempStack.Children.Add(_mbTemp);
        mbMetricsGrid.Children.Add(mbTempStack);

        var mbVrmStack = new StackPanel { Spacing = 2 };
        mbVrmStack.Children.Add(new TextBlock { Text = "VRM", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _mbVrmTemp = new TextBlock { FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) };
        mbVrmStack.Children.Add(_mbVrmTemp);
        Grid.SetColumn(mbVrmStack, 1);
        mbMetricsGrid.Children.Add(mbVrmStack);
        mbStack.Children.Add(mbMetricsGrid);

        // MB Fans
        _mbFansPanel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 4, 0, 0) };
        mbStack.Children.Add(_mbFansPanel);

        mbCard.Child = mbStack;
        Grid.SetColumn(mbCard, 0);
        row1.Children.Add(mbCard);

        // RAM Card
        var ramCard = CreateCard();
        var ramStack = new StackPanel { Spacing = 12 };
        var ramHeader = CreateCardHeaderGrid("\uf515", "RAM", RamColor);
        _ramVoltage = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(ColorFromHex("#9CA3AF")), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_ramVoltage, 1);
        ((Grid)ramHeader).Children.Add(_ramVoltage);
        ramStack.Children.Add(ramHeader);
        _ramModel = new TextBlock { FontSize = 11, Foreground = SubtleText, TextTrimming = TextTrimming.CharacterEllipsis };
        ramStack.Children.Add(_ramModel);
        var ramMetricsGrid = new Grid { ColumnSpacing = 24 };
        ramMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ramMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var ramUsageStack = new StackPanel { Spacing = 2 };
        ramUsageStack.Children.Add(new TextBlock { Text = "USO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _ramPct = new TextBlock { FontSize = 32, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = RamColor };
        ramUsageStack.Children.Add(_ramPct);
        ramMetricsGrid.Children.Add(ramUsageStack);
        var ramInfoStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        _ramInfo = new TextBlock { FontSize = 13, Foreground = new SolidColorBrush(Colors.White), HorizontalAlignment = HorizontalAlignment.Right };
        ramInfoStack.Children.Add(_ramInfo);
        Grid.SetColumn(ramInfoStack, 1);
        ramMetricsGrid.Children.Add(ramInfoStack);
        ramStack.Children.Add(ramMetricsGrid);
        ramStack.Children.Add(CreateBar(out _ramBar, RamColor));
        ramCard.Child = ramStack;
        Grid.SetColumn(ramCard, 1);
        row1.Children.Add(ramCard);
        contentStack.Children.Add(row1);

        // ===== ROW 2: CPU + GPU =====
        var row2 = new Grid { ColumnSpacing = 16 };
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // CPU Card
        var cpuCard = CreateCard();
        var cpuStack = new StackPanel { Spacing = 12 };
        var cpuHeader = CreateCardHeaderGrid("\uef8e", "CPU", CpuColor);
        _cpuVoltage = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(ColorFromHex("#9CA3AF")), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_cpuVoltage, 1);
        ((Grid)cpuHeader).Children.Add(_cpuVoltage);
        cpuStack.Children.Add(cpuHeader);
        _cpuModel = new TextBlock { FontSize = 11, Foreground = SubtleText, TextTrimming = TextTrimming.CharacterEllipsis };
        cpuStack.Children.Add(_cpuModel);

        var cpuMetricsGrid = new Grid { ColumnSpacing = 24 };
        cpuMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cpuMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cpuMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var cpuUsageStack = new StackPanel { Spacing = 2 };
        cpuUsageStack.Children.Add(new TextBlock { Text = "USO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _cpuPct = new TextBlock { FontSize = 32, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = CpuColor };
        cpuUsageStack.Children.Add(_cpuPct);
        cpuMetricsGrid.Children.Add(cpuUsageStack);
        var cpuCoreStack = new StackPanel { Spacing = 2 };
        cpuCoreStack.Children.Add(new TextBlock { Text = "CORE", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _cpuCoreTemp = new TextBlock { FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) };
        cpuCoreStack.Children.Add(_cpuCoreTemp);
        Grid.SetColumn(cpuCoreStack, 1);
        cpuMetricsGrid.Children.Add(cpuCoreStack);
        var cpuPkgStack = new StackPanel { Spacing = 2 };
        cpuPkgStack.Children.Add(new TextBlock { Text = "PKG", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _cpuPkgTemp = new TextBlock { FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) };
        cpuPkgStack.Children.Add(_cpuPkgTemp);
        Grid.SetColumn(cpuPkgStack, 2);
        cpuMetricsGrid.Children.Add(cpuPkgStack);
        cpuStack.Children.Add(cpuMetricsGrid);
        cpuStack.Children.Add(CreateBar(out _cpuBar, CpuColor));

        var cpuGraphContainer = new Border { Background = new SolidColorBrush(ColorFromHex("#0B1120")), CornerRadius = new CornerRadius(8), Height = 50, Padding = new Thickness(8), BorderThickness = new Thickness(1), BorderBrush = CardBorder };
        _cpuGraph = new Canvas { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        cpuGraphContainer.Child = _cpuGraph;
        cpuStack.Children.Add(cpuGraphContainer);

        // CPU Fans
        _cpuFansPanel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 4, 0, 0) };
        cpuStack.Children.Add(_cpuFansPanel);

        cpuCard.Child = cpuStack;
        Grid.SetColumn(cpuCard, 0);
        row2.Children.Add(cpuCard);

        // GPU Card
        var gpuCard = CreateCard();
        var gpuStack = new StackPanel { Spacing = 12 };
        var gpuHeader = CreateCardHeaderGrid("\uea89", "GPU", GpuColor);
        _gpuVoltage = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(ColorFromHex("#9CA3AF")), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_gpuVoltage, 1);
        ((Grid)gpuHeader).Children.Add(_gpuVoltage);
        gpuStack.Children.Add(gpuHeader);
        _gpuModel = new TextBlock { FontSize = 11, Foreground = SubtleText, TextTrimming = TextTrimming.CharacterEllipsis };
        gpuStack.Children.Add(_gpuModel);

        var gpuMetricsGrid = new Grid { ColumnSpacing = 24 };
        gpuMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gpuMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var gpuUsageStack = new StackPanel { Spacing = 2 };
        gpuUsageStack.Children.Add(new TextBlock { Text = "USO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _gpuPct = new TextBlock { FontSize = 32, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = GpuColor };
        gpuUsageStack.Children.Add(_gpuPct);
        gpuMetricsGrid.Children.Add(gpuUsageStack);
        var gpuTempStack = new StackPanel { Spacing = 2 };
        gpuTempStack.Children.Add(new TextBlock { Text = "TEMPERATURA", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _gpuCoreTemp = new TextBlock { FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) };
        gpuTempStack.Children.Add(_gpuCoreTemp);
        Grid.SetColumn(gpuTempStack, 1);
        gpuMetricsGrid.Children.Add(gpuTempStack);
        gpuStack.Children.Add(gpuMetricsGrid);
        gpuStack.Children.Add(CreateBar(out _gpuBar, GpuColor));

        var gpuGraphContainer = new Border { Background = new SolidColorBrush(ColorFromHex("#0B1120")), CornerRadius = new CornerRadius(8), Height = 50, Padding = new Thickness(8), BorderThickness = new Thickness(1), BorderBrush = CardBorder };
        _gpuGraph = new Canvas { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        gpuGraphContainer.Child = _gpuGraph;
        gpuStack.Children.Add(gpuGraphContainer);

        // GPU Fans
        _gpuFansPanel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 4, 0, 0) };
        gpuStack.Children.Add(_gpuFansPanel);

        gpuCard.Child = gpuStack;
        Grid.SetColumn(gpuCard, 1);
        row2.Children.Add(gpuCard);
        contentStack.Children.Add(row2);

        // ===== ROW 3: Disk + Network =====
        var row3 = new Grid { ColumnSpacing = 16 };
        row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Disk Card
        var diskCard = CreateCard();
        var diskStack = new StackPanel { Spacing = 12 };
        diskStack.Children.Add(CreateCardHeader("\uEDA2", "DISCO", DiskColor));
        _diskName = new TextBlock { FontSize = 11, Foreground = SubtleText, TextTrimming = TextTrimming.CharacterEllipsis };
        diskStack.Children.Add(_diskName);

        var diskMetricsGrid = new Grid { ColumnSpacing = 24 };
        diskMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        diskMetricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var diskUsageStack = new StackPanel { Spacing = 2 };
        diskUsageStack.Children.Add(new TextBlock { Text = "USO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _diskPct = new TextBlock { FontSize = 32, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = DiskColor };
        diskUsageStack.Children.Add(_diskPct);
        diskMetricsGrid.Children.Add(diskUsageStack);

        var diskInfoStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        _diskInfo = new TextBlock { FontSize = 13, Foreground = new SolidColorBrush(Colors.White), HorizontalAlignment = HorizontalAlignment.Right };
        diskInfoStack.Children.Add(_diskInfo);
        Grid.SetColumn(diskInfoStack, 1);
        diskMetricsGrid.Children.Add(diskInfoStack);

        diskStack.Children.Add(diskMetricsGrid);
        diskStack.Children.Add(CreateBar(out _diskBar, DiskColor));

        // Disk Read/Write speeds
        var diskSpeedGrid = new Grid { ColumnSpacing = 24, Margin = new Thickness(0, 4, 0, 0) };
        diskSpeedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        diskSpeedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var readStack = new StackPanel { Spacing = 2 };
        readStack.Children.Add(new TextBlock { Text = "LEITURA", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _diskRead = new TextBlock { FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) };
        readStack.Children.Add(_diskRead);
        diskSpeedGrid.Children.Add(readStack);

        var writeStack = new StackPanel { Spacing = 2 };
        writeStack.Children.Add(new TextBlock { Text = "ESCRITA", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _diskWrite = new TextBlock { FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = DiskColor };
        writeStack.Children.Add(_diskWrite);
        Grid.SetColumn(writeStack, 1);
        diskSpeedGrid.Children.Add(writeStack);

        diskStack.Children.Add(diskSpeedGrid);

        diskCard.Child = diskStack;
        Grid.SetColumn(diskCard, 0);
        row3.Children.Add(diskCard);

        // Network Card
        var netCard = CreateCard();
        var netStack = new StackPanel { Spacing = 12 };
        netStack.Children.Add(CreateCardHeader("\uE8B0", "REDE", NetColor));
        _netName = new TextBlock { FontSize = 11, Foreground = SubtleText, TextTrimming = TextTrimming.CharacterEllipsis };
        netStack.Children.Add(_netName);

        var netGrid = new Grid { ColumnSpacing = 24 };
        netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var downStack = new StackPanel { Spacing = 2 };
        downStack.Children.Add(new TextBlock { Text = "DOWNLOAD", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _netDown = new TextBlock { FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = NetColor };
        downStack.Children.Add(_netDown);
        netGrid.Children.Add(downStack);

        var upStack = new StackPanel { Spacing = 2 };
        upStack.Children.Add(new TextBlock { Text = "UPLOAD", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _netUp = new TextBlock { FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) };
        upStack.Children.Add(_netUp);
        Grid.SetColumn(upStack, 1);
        netGrid.Children.Add(upStack);

        netStack.Children.Add(netGrid);

        // Network graphs
        var netGraphGrid = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 4, 0, 0), Height = 50 };
        netGraphGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        netGraphGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var downGraphBg = new Border { Background = new SolidColorBrush(ColorFromHex("#0B1120")), CornerRadius = new CornerRadius(6), Padding = new Thickness(4), BorderThickness = new Thickness(1), BorderBrush = CardBorder };
        _netDownGraph = new Canvas { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        downGraphBg.Child = _netDownGraph;
        netGraphGrid.Children.Add(downGraphBg);

        var upGraphBg = new Border { Background = new SolidColorBrush(ColorFromHex("#0B1120")), CornerRadius = new CornerRadius(6), Padding = new Thickness(4), BorderThickness = new Thickness(1), BorderBrush = CardBorder };
        _netUpGraph = new Canvas { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        upGraphBg.Child = _netUpGraph;
        Grid.SetColumn(upGraphBg, 1);
        netGraphGrid.Children.Add(upGraphBg);

        netStack.Children.Add(netGraphGrid);

        netCard.Child = netStack;
        Grid.SetColumn(netCard, 1);
        row3.Children.Add(netCard);
        contentStack.Children.Add(row3);

        // ===== ROW 4: FPS =====
        _fpsCard = CreateCard();
        var fpsStack = new StackPanel { Spacing = 12 };
        fpsStack.Children.Add(CreateCardHeader("\uec4d", "FPS", FpsColor));
        var fpsGrid = new Grid { ColumnSpacing = 32 };
        fpsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        fpsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var fpsMainStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        _fpsValue = new TextBlock { FontSize = 48, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = FpsColor };
        fpsMainStack.Children.Add(_fpsValue);
        _fpsLabel = new TextBlock { FontSize = 11, Foreground = SubtleText };
        fpsMainStack.Children.Add(_fpsLabel);
        fpsGrid.Children.Add(fpsMainStack);
        var fpsDetailsStack = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        _fpsInfo = new TextBlock { FontSize = 13, Foreground = new SolidColorBrush(Colors.White) };
        fpsDetailsStack.Children.Add(_fpsInfo);
        var fpsStatsGrid = new Grid { ColumnSpacing = 16 };
        fpsStatsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fpsStatsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fpsStatsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fpsStatsGrid.Children.Add(CreateStatItem("1% LOW", "--", SubtleText));
        var stat2 = CreateStatItem("0.1% LOW", "--", SubtleText); Grid.SetColumn(stat2, 1); fpsStatsGrid.Children.Add(stat2);
        var stat3 = CreateStatItem("FRAME TIME", "--", SubtleText); Grid.SetColumn(stat3, 2); fpsStatsGrid.Children.Add(stat3);
        fpsDetailsStack.Children.Add(fpsStatsGrid);
        Grid.SetColumn(fpsDetailsStack, 1);
        fpsGrid.Children.Add(fpsDetailsStack);
        fpsStack.Children.Add(fpsGrid);
        _fpsCard.Child = fpsStack;
        contentStack.Children.Add(_fpsCard);

        mainContent.Content = contentStack;
        Grid.SetColumn(mainContent, 1);
        root.Children.Add(mainContent);
        Content = root;

        Closed += (_, _) => { _timer.Stop(); _graphTimer.Stop(); _monitor.Dispose(); };
        appWindow.Changed += (_, args) =>
        {
            if (args.DidPositionChange) { if (!_isMoving) { _isMoving = true; _timer.Stop(); _graphTimer.Stop(); } }
            else if (_isMoving) { _isMoving = false; _timer.Start(); _graphTimer.Start(); }
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        _graphTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        _graphTimer.Tick += (_, _) => UpdateGraphs();
        _graphTimer.Start();

        Refresh();
    }

    private static Border CreateCard() => new()
    {
        Background = CardBg, CornerRadius = new CornerRadius(12),
        Padding = new Thickness(20), BorderThickness = new Thickness(1), BorderBrush = CardBorder
    };

    private static StackPanel CreateCardHeader(string icon, string title, SolidColorBrush accentColor)
    {
        var hdr = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        hdr.Children.Add(new FontIcon { Glyph = icon, FontSize = 18, Foreground = accentColor, FontFamily = new FontFamily("Assets/tabler-icons.ttf#tabler-icons") });
        hdr.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White), VerticalAlignment = VerticalAlignment.Center });
        return hdr;
    }

    private static StackPanel CreateStatItem(string label, string value, SolidColorBrush valueColor)
    {
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = new SolidColorBrush(ColorFromHex("#475569")), CharacterSpacing = 50 });
        stack.Children.Add(new TextBlock { Text = value, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = valueColor });
        return stack;
    }

    private static Grid CreateCardHeaderGrid(string icon, string title, SolidColorBrush accentColor)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        
        var hdr = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        hdr.Children.Add(new FontIcon { Glyph = icon, FontSize = 18, Foreground = accentColor, FontFamily = new FontFamily("Assets/tabler-icons.ttf#tabler-icons") });
        hdr.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White), VerticalAlignment = VerticalAlignment.Center });
        grid.Children.Add(hdr);
        return grid;
    }

    private static Grid CreateBar(out Grid bar, SolidColorBrush color)
    {
        var fill = new Border { Background = color, CornerRadius = new CornerRadius(4), Height = 6 };
        var bg = new Border { Background = new SolidColorBrush(ColorFromHex("#1E293B")), CornerRadius = new CornerRadius(4), Height = 6 };
        bar = new Grid();
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100, GridUnitType.Star) });
        Grid.SetColumn(fill, 0); Grid.SetColumn(bg, 1);
        bar.Children.Add(fill); bar.Children.Add(bg);
        return bar;
    }

    private static Button CreateNavItem(string icon, string label, bool isActive, SolidColorBrush accentColor)
    {
        var btn = new Button
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = isActive ? new SolidColorBrush(ColorFromHex("#1A2744")) : new SolidColorBrush(Colors.Transparent),
            Foreground = isActive ? new SolidColorBrush(Colors.White) : new SolidColorBrush(ColorFromHex("#94A3B8")),
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 2, 0, 2)
        };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        panel.Children.Add(new FontIcon { Glyph = icon, FontSize = 16, Foreground = isActive ? accentColor : new SolidColorBrush(ColorFromHex("#64748B")), FontFamily = new FontFamily("Assets/tabler-icons.ttf#tabler-icons") });
        panel.Children.Add(new TextBlock { Text = label, FontSize = 13, FontWeight = isActive ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center });
        btn.Content = panel;
        return btn;
    }

    private void Refresh()
    {
        try
        {
            var s = _monitor.Collect();

            // Motherboard
            _mbModel.Text = s.MotherboardModel;
            _mbTemp.Text = s.MotherboardTemp > 0 ? $"{s.MotherboardTemp:F0}°C" : "--";
            _mbTemp.Foreground = TempColor(s.MotherboardTemp);
            _mbVrmTemp.Text = s.MotherboardVrmTemp > 0 ? $"{s.MotherboardVrmTemp:F0}°C" : "--";
            _mbVrmTemp.Foreground = TempColor(s.MotherboardVrmTemp);
            _mbVoltage.Text = s.MotherboardVoltage > 0 ? $"{s.MotherboardVoltage:F2}V" : "";

            // RAM
            var rp = s.TotalRam > 0 ? s.UsedRam / s.TotalRam * 100 : 0;
            _ramModel.Text = s.RamModuleCount > 0 ? $"{s.RamModuleCount}x{FmtSize(s.RamModuleSize)} {s.RamModel} @ {s.RamSpeed} MHz" : $"{s.RamModel} @ {s.RamSpeed} MHz";
            _ramVoltage.Text = s.RamVoltage > 0 ? $"{s.RamVoltage:F1}V" : "";
            _ramInfo.Text = $"{s.UsedRam:F1} / {s.TotalRam:F1} GB";
            _ramPct.Text = $"{rp:F0}%";
            _ramPct.Foreground = UsageColor(rp, RamColor);
            SetBar(_ramBar, rp, UsageColor(rp, RamColor));

            // CPU
            _cpuModel.Text = s.CpuModel;
            var cpuPowerStr = s.CpuVoltage > 0 ? $"{s.CpuVoltage:F3}V" : "";
            if (s.CpuPower > 0) cpuPowerStr += $" | {s.CpuPower:F0}W";
            _cpuVoltage.Text = cpuPowerStr;
            _cpuPct.Text = $"{s.CpuUsage:F0}%";
            _cpuPct.Foreground = UsageColor(s.CpuUsage, CpuColor);
            SetBar(_cpuBar, s.CpuUsage, UsageColor(s.CpuUsage, CpuColor));
            _cpuCoreTemp.Text = s.CpuTemp > 0 ? $"{s.CpuTemp:F0}°C" : "--";
            _cpuCoreTemp.Foreground = TempColor(s.CpuTemp);
            _cpuPkgTemp.Text = s.CpuPackageTemp > 0 ? $"{s.CpuPackageTemp:F0}°C" : "--";
            _cpuPkgTemp.Foreground = TempColor(s.CpuPackageTemp);
            _cpuHistory.Add(s.CpuUsage);
            if (_cpuHistory.Count > 60) _cpuHistory.RemoveAt(0);

            // GPU
            _gpuModel.Text = s.GpuModel;
            var gpuPowerStr = s.GpuVoltage > 0 ? $"{s.GpuVoltage:F3}V" : "";
            if (s.GpuPower > 0) gpuPowerStr += $" | {s.GpuPower:F0}W";
            _gpuVoltage.Text = gpuPowerStr;
            _gpuPct.Text = $"{s.GpuUsage:F0}%";
            _gpuPct.Foreground = UsageColor(s.GpuUsage, GpuColor);
            SetBar(_gpuBar, s.GpuUsage, UsageColor(s.GpuUsage, GpuColor));
            _gpuCoreTemp.Text = s.GpuTemp > 0 ? $"{s.GpuTemp:F0}°C" : "--";
            _gpuCoreTemp.Foreground = TempColor(s.GpuTemp);
            _gpuHistory.Add(s.GpuUsage);
            if (_gpuHistory.Count > 60) _gpuHistory.RemoveAt(0);
            _diskName.Text = s.DiskName;
            _diskPct.Text = s.DiskUsagePercent > 0 ? $"{s.DiskUsagePercent:F0}%" : "--";
            _diskPct.Foreground = UsageColor(s.DiskUsagePercent, DiskColor);
            SetBar(_diskBar, s.DiskUsagePercent, UsageColor(s.DiskUsagePercent, DiskColor));
            _diskInfo.Text = s.DiskTotalGb > 0 ? $"{s.DiskUsedGb:F1} / {s.DiskTotalGb:F1} GB" : "--";
            _diskRead.Text = s.DiskReadKbps > 0 ? FormatSpeed(s.DiskReadKbps) : "--";
            _diskWrite.Text = s.DiskWriteKbps > 0 ? FormatSpeed(s.DiskWriteKbps) : "--";

            // Network (with anti-flicker - cache last value)
            _netName.Text = s.NetworkName;
            var netDown = s.NetworkDownloadSpeed > 0 ? s.NetworkDownloadSpeed : _lastNetDown;
            var netUp = s.NetworkUploadSpeed > 0 ? s.NetworkUploadSpeed : _lastNetUp;
            _lastNetDown = netDown;
            _lastNetUp = netUp;
            _netDown.Text = netDown > 0 ? FormatSpeed(netDown) : "--";
            _netUp.Text = netUp > 0 ? FormatSpeed(netUp) : "--";
            _netDownHistory.Add(netDown);
            _netUpHistory.Add(netUp);
            if (_netDownHistory.Count > 60) _netDownHistory.RemoveAt(0);
            if (_netUpHistory.Count > 60) _netUpHistory.RemoveAt(0);

            // Fans - distribuir para cada card
            if (s.Fans.Count != _fanCount)
            {
                _fanCount = s.Fans.Count;
                _mbFansPanel.Children.Clear();
                _cpuFansPanel.Children.Clear();
                _gpuFansPanel.Children.Clear();

                var mbFans = new List<FanInfo>();
                var cpuFans = new List<FanInfo>();
                var gpuFans = new List<FanInfo>();

                foreach (var f in s.Fans)
                {
                    if (f.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase)) gpuFans.Add(f);
                    else if (f.Name.Contains("CPU", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("OPT_CPU", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Bomba", StringComparison.OrdinalIgnoreCase)) cpuFans.Add(f);
                    else mbFans.Add(f);
                }

                if (mbFans.Count > 0) PopulateFanPanel(_mbFansPanel, mbFans, MbColor);
                if (cpuFans.Count > 0) PopulateFanPanel(_cpuFansPanel, cpuFans, CpuColor);
                if (gpuFans.Count > 0) PopulateFanPanel(_gpuFansPanel, gpuFans, GpuColor);
            }
            else if (s.Fans.Count > 0)
            {
                UpdateFanRpms(s.Fans);
            }

            // FPS
            if (s.FpsCurrent > 0)
            {
                _fpsValue.Text = $"{s.FpsCurrent:F0}";
                _fpsValue.Foreground = FpsValueColor(s.FpsCurrent);
                _fpsLabel.Text = "FRAMES PER SECOND";
                _fpsInfo.Text = $"MIN {s.FpsMin:F0}  ·  AVG {s.FpsAvg:F0}  ·  MAX {s.FpsMax:F0}  ·  {s.FpsSource}";
            }
            else
            {
                _fpsValue.Text = "--";
                _fpsValue.Foreground = SubtleText;
                _fpsLabel.Text = s.FpsSource != "Off" ? $"Aguardando... {s.FpsSource}" : "Nenhum jogo detectado";
                _fpsInfo.Text = "";
            }
        }
        catch { }
    }

    private void PopulateFanPanel(StackPanel panel, List<FanInfo> fans, SolidColorBrush accentColor)
    {
        if (fans.Count >= 2)
        {
            // Grid com colunas para 2+ fans lado a lado
            var fanGrid = new Grid { ColumnSpacing = 8 };
            for (int i = 0; i < Math.Min(fans.Count, 2); i++)
                fanGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < Math.Min(fans.Count, 2); i++)
            {
                var f = fans[i];
                var fanCell = new Border { Background = new SolidColorBrush(ColorFromHex("#0B1120")), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6, 10, 6), BorderThickness = new Thickness(1), BorderBrush = CardBorder };
                var cellGrid = new Grid { ColumnSpacing = 8 };
                cellGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                cellGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                cellGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var fanIcon = new FontIcon { Glyph = "\ueec4", FontSize = 14, Foreground = SubtleText, FontFamily = new FontFamily("Assets/tabler-icons.ttf#tabler-icons"), RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5), VerticalAlignment = VerticalAlignment.Center };
                var speed = Math.Max(0.3, Math.Min(3, f.Rpm / 500.0));
                fanIcon.Loaded += (_, _) => { var visual = ElementCompositionPreview.GetElementVisual(fanIcon); visual.CenterPoint = new Vector3(7f, 7f, 0); SpinForever(visual, speed); };
                cellGrid.Children.Add(fanIcon);

                var fanName = new TextBlock { Text = f.Name, FontSize = 10, Foreground = SubtleText, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(fanName, 1); cellGrid.Children.Add(fanName);

                var fanRpm = new TextBlock { Text = $"{f.Rpm:F0} RPM", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = accentColor, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(fanRpm, 2); cellGrid.Children.Add(fanRpm);

                fanCell.Child = cellGrid;
                Grid.SetColumn(fanCell, i);
                fanGrid.Children.Add(fanCell);
            }
            panel.Children.Add(fanGrid);
        }
        else
        {
            // Uma fan - layout simples
            foreach (var f in fans)
            {
                var fanRow = new Border { Background = new SolidColorBrush(ColorFromHex("#0B1120")), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6, 10, 6), BorderThickness = new Thickness(1), BorderBrush = CardBorder };
                var fanGrid = new Grid { ColumnSpacing = 10 };
                fanGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                fanGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fanGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var fanIcon = new FontIcon { Glyph = "\ueec4", FontSize = 16, Foreground = SubtleText, FontFamily = new FontFamily("Assets/tabler-icons.ttf#tabler-icons"), RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5), VerticalAlignment = VerticalAlignment.Center };
                var speed = Math.Max(0.3, Math.Min(3, f.Rpm / 500.0));
                fanIcon.Loaded += (_, _) => { var visual = ElementCompositionPreview.GetElementVisual(fanIcon); visual.CenterPoint = new Vector3(8f, 8f, 0); SpinForever(visual, speed); };
                fanGrid.Children.Add(fanIcon);

                var fanName = new TextBlock { Text = f.Name, FontSize = 11, Foreground = SubtleText, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(fanName, 1); fanGrid.Children.Add(fanName);

                var fanRpm = new TextBlock { Text = $"{f.Rpm:F0} RPM", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = accentColor, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(fanRpm, 2); fanGrid.Children.Add(fanRpm);

                fanRow.Child = fanGrid;
                panel.Children.Add(fanRow);
            }
        }
    }

    private void UpdateFanRpms(List<FanInfo> fans)
    {
        UpdatePanelRpms(_mbFansPanel, fans, "CHAN", MbColor);
        UpdatePanelRpms(_cpuFansPanel, fans, "CPU", CpuColor);
        UpdatePanelRpms(_gpuFansPanel, fans, "GPU", GpuColor);
    }

    private static void UpdatePanelRpms(StackPanel panel, List<FanInfo> fans, string filter, SolidColorBrush color)
    {
        foreach (var child in panel.Children)
        {
            if (child is Grid grid && grid.Children.Count >= 2)
            {
                // Grid com 2 fans lado a lado
                for (int i = 0; i < grid.Children.Count; i++)
                {
                    if (grid.Children[i] is Border cell && cell.Child is Grid cellGrid && cellGrid.Children.Count > 2)
                    {
                        var nameBlock = cellGrid.Children[1] as TextBlock;
                        var rpmBlock = cellGrid.Children[2] as TextBlock;
                        if (nameBlock != null)
                        {
                            var match = fans.FirstOrDefault(f => f.Name == nameBlock.Text);
                            if (match != null && rpmBlock != null)
                            {
                                rpmBlock.Text = $"{match.Rpm:F0} RPM";
                                rpmBlock.Foreground = color;
                            }
                        }
                    }
                }
            }
            else if (child is Border border && border.Child is Grid singleGrid && singleGrid.Children.Count > 2)
            {
                // Uma fan - layout simples
                var nameBlock = singleGrid.Children[1] as TextBlock;
                var rpmBlock = singleGrid.Children[2] as TextBlock;
                if (nameBlock != null)
                {
                    var match = fans.FirstOrDefault(f => f.Name == nameBlock.Text);
                    if (match != null && rpmBlock != null)
                    {
                        rpmBlock.Text = $"{match.Rpm:F0} RPM";
                        rpmBlock.Foreground = color;
                    }
                }
            }
        }
    }

    private void UpdateGraphs()
    {
        DrawGraph(_cpuGraph, _cpuHistory, CpuColor, 100);
        DrawGraph(_gpuGraph, _gpuHistory, GpuColor, 100);
        DrawGraph(_netDownGraph, _netDownHistory, NetColor, 1024);
        DrawGraph(_netUpGraph, _netUpHistory, new SolidColorBrush(Colors.White), 1024);
    }

    private static void DrawGraph(Canvas canvas, List<double> data, SolidColorBrush color, double maxVal)
    {
        canvas.Children.Clear();
        if (data.Count < 2) return;
        var w = canvas.ActualWidth; var h = canvas.ActualHeight;
        if (w <= 0 || h <= 0) return;
        var stepX = w / (data.Count - 1);

        var fillPath = new Microsoft.UI.Xaml.Shapes.Path { Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(40, color.Color.R, color.Color.G, color.Color.B)), Stroke = null };
        var fillGeometry = new PathGeometry();
        var fillFigure = new PathFigure { IsClosed = true };
        fillFigure.StartPoint = new Windows.Foundation.Point(0, h);
        for (int i = 0; i < data.Count; i++) fillFigure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(i * stepX, h - (data[i] / maxVal * h)) });
        fillFigure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point((data.Count - 1) * stepX, h) });
        fillGeometry.Figures.Add(fillFigure); fillPath.Data = fillGeometry; canvas.Children.Add(fillPath);

        var linePath = new Microsoft.UI.Xaml.Shapes.Path { Stroke = color, StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round };
        var lineGeometry = new PathGeometry();
        var lineFigure = new PathFigure { StartPoint = new Windows.Foundation.Point(0, h - (data[0] / maxVal * h)) };
        for (int i = 1; i < data.Count; i++) lineFigure.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(i * stepX, h - (data[i] / maxVal * h)) });
        lineGeometry.Figures.Add(lineFigure); linePath.Data = lineGeometry; canvas.Children.Add(linePath);
    }

    private static SolidColorBrush UsageColor(double pct, SolidColorBrush baseColor)
    {
        if (pct >= 85) return SteamColors.RedBrush;
        if (pct >= 60) return new SolidColorBrush(ColorFromHex("#F59E0B"));
        return baseColor;
    }

    private static SolidColorBrush TempColor(double t)
    {
        if (t >= 85) return SteamColors.RedBrush;
        if (t >= 70) return new SolidColorBrush(ColorFromHex("#F59E0B"));
        if (t >= 50) return SteamColors.GreenBrush;
        return SteamColors.BlueBrush;
    }

    private static SolidColorBrush FpsValueColor(double fps)
    {
        if (fps >= 60) return new SolidColorBrush(ColorFromHex("#4ADE80"));
        if (fps >= 30) return new SolidColorBrush(ColorFromHex("#FBBF24"));
        return new SolidColorBrush(ColorFromHex("#EF4444"));
    }

    private static void SetBar(Grid bar, double pct, SolidColorBrush color)
    {
        pct = Math.Max(0, Math.Min(100, pct));
        bar.ColumnDefinitions[0].Width = new GridLength(pct, GridUnitType.Star);
        bar.ColumnDefinitions[1].Width = new GridLength(100 - pct, GridUnitType.Star);
        ((Border)bar.Children[0]).Background = color;
    }

    private static void SpinForever(Visual visual, double speed)
    {
        var anim = visual.Compositor.CreateScalarKeyFrameAnimation();
        anim.InsertKeyFrame(0, 0); anim.InsertKeyFrame(1, 360);
        anim.Duration = TimeSpan.FromSeconds(speed);
        var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        visual.StartAnimation("RotationAngleInDegrees", anim);
        batch.Completed += (_, _) => SpinForever(visual, speed);
        batch.End();
    }

    private static Windows.UI.Color ColorFromHex(string hex)
    {
        hex = hex.TrimStart('#');
        return Windows.UI.Color.FromArgb(255, byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber), byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber), byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber));
    }

    private static string FmtSize(double gb) => gb >= 1024 ? $"{gb / 1024:F0}TB" : $"{gb:F0}GB";

    private static string FormatSpeed(double kbps)
    {
        if (kbps >= 1024) return $"{kbps / 1024:F1} MB/s";
        return $"{kbps:F0} KB/s";
    }
}
