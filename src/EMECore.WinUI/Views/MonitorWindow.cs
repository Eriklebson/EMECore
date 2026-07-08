using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System.Numerics;
using System.Collections.ObjectModel;
using EMECore.Core.Models;
using EMECore.Hardware.Services;
using EMECore.WinUI.Theme;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WinUI;
using SkiaSharp;

namespace EMECore.WinUI.Views;

public sealed partial class MonitorWindow : Window
{
    private readonly HardwareMonitorService _monitor;
    private readonly StressTestService _stressTest = new();
    private readonly MappingService _mapping = MappingService.Instance;
    private DispatcherTimer _graphTimer = null!;
    private DispatcherTimer _stressTimer = null!;
    private System.Threading.Timer? _bgTimer;

    // Motherboard
    private TextBlock _mbModel = null!, _mbTemp = null!, _mbVrmTemp = null!, _mbVoltage = null!;
    private StackPanel _mbFansPanel = null!;
    private TextBlock _mbCompactTemp = null!, _mbCompactVrm = null!, _mbCompactFans = null!, _mbCompactVoltage = null!;

    // RAM
    private TextBlock _ramPct = null!, _ramInfo = null!, _ramModel = null!, _ramVoltage = null!;
    private Grid _ramBar = null!;
    private TextBlock _ramCompactPct = null!, _ramCompactInfo = null!;

    // CPU
    private TextBlock _cpuPct = null!, _cpuCoreTemp = null!, _cpuPkgTemp = null!, _cpuModel = null!, _cpuVoltage = null!;
    private Grid _cpuBar = null!;
    private CartesianChart _cpuChart = null!;
    private readonly ObservableCollection<double> _cpuValues = new();
    private StackPanel _cpuFansPanel = null!;
    private TextBlock _cpuCompactPct = null!, _cpuCompactCore = null!, _cpuCompactPkg = null!;

    // GPU
    private TextBlock _gpuPct = null!, _gpuCoreTemp = null!, _gpuModel = null!, _gpuVoltage = null!;
    private Grid _gpuBar = null!;
    private CartesianChart _gpuChart = null!;
    private readonly ObservableCollection<double> _gpuValues = new();
    private StackPanel _gpuFansPanel = null!;
    private TextBlock _gpuCompactPct = null!, _gpuCompactTemp = null!;

    // Disk (dynamic multi-disk)
    private StackPanel _disksPanel = null!;
    private TextBlock _diskCompactInfo = null!;

    // Network
    private TextBlock _netName = null!, _netDown = null!, _netUp = null!;
    private CartesianChart _netDownChart = null!, _netUpChart = null!;
    private readonly ObservableCollection<double> _netDownValues = new();
    private readonly ObservableCollection<double> _netUpValues = new();
    private double _lastNetDown, _lastNetUp;
    private TextBlock _netCompactDown = null!, _netCompactUp = null!;

    // FPS
    private TextBlock _fpsValue = null!, _fpsInfo = null!, _fpsLabel = null!;
    private Border _fpsCard = null!;
    private Button _fpsToggleBtn = null!;

    private int _fanCount;
    private bool _isMoving;

    // Navigation
    private Button _navHardware = null!;
    private Button _navStressTest = null!;
    private Button _navMonitores = null!;
    private Button _navPerifericos = null!;
    private ScrollViewer _hardwareContent = null!;
    private ScrollViewer _stressTestContent = null!;
    private ScrollViewer _monitoresContent = null!;
    private ScrollViewer _perifericosContent = null!;

    // Stress Test UI
    private TextBlock _cpuStressModel = null!;
    private TextBlock _cpuStressStatus = null!;
    private TextBlock _cpuStressTime = null!;
    private TextBlock _cpuStressLoad = null!;
    private TextBlock _cpuStressTempValue = null!;
    private TextBlock _cpuStressLoadValue = null!;
    private Button _cpuStressBtn = null!;
    private TextBlock _gpuStressModel = null!;
    private TextBlock _gpuStressStatus = null!;
    private TextBlock _gpuStressTemp = null!;
    private TextBlock _gpuStressUsage = null!;
    private TextBlock _gpuStressTempValue = null!;
    private TextBlock _gpuStressLoadValue = null!;
    private Button _gpuStressBtn = null!;
    private Border _cpuStressIndicator = null!;
    private Border _gpuStressIndicator = null!;
    private TextBlock _furmarkStatus = null!;

    // Stress Test Graphs
    private CartesianChart _cpuStressTempChart = null!;
    private CartesianChart _cpuStressUsageChart = null!;
    private readonly ObservableCollection<double> _cpuStressTempValues = new();
    private readonly ObservableCollection<double> _cpuStressUsageValues = new();
    private CartesianChart _gpuStressTempChart = null!;
    private CartesianChart _gpuStressUsageChart = null!;
    private readonly ObservableCollection<double> _gpuStressTempValues = new();
    private readonly ObservableCollection<double> _gpuStressUsageValues = new();

    // Gamepad real-time
    private DispatcherTimer _gamepadTimer = null!;
    private Border _gpBtnA = null!, _gpBtnB = null!, _gpBtnX = null!, _gpBtnY = null!;
    private Border _gpBtnLB = null!, _gpBtnRB = null!;
    private Border _gpBtnStart = null!, _gpBtnBack = null!;
    private Border _gpBtnUp = null!, _gpBtnDown = null!, _gpBtnLeft = null!, _gpBtnRight = null!;
    private Border _gpBtnLS = null!, _gpBtnRS = null!;
    private TextBlock _gpPollingText = null!;
    private TextBlock _gpLxText = null!, _gpLyText = null!, _gpRxText = null!, _gpRyText = null!;
    private TextBlock _gpLtText = null!, _gpRtText = null!;
    private GamepadInfo _lastGamepadState = new();

    // Cached hardware data — Collect() result reused by StressMetrics
    private HardwareStats? _lastCollect;

    // Card collapse state (key = card name, value = isCollapsed)
    private readonly Dictionary<string, bool> _collapsedState = new();
    private readonly Dictionary<string, StackPanel> _expandedContent = new();
    private readonly Dictionary<string, StackPanel> _collapsedContent = new();
    private readonly Dictionary<string, Border> _cardBorders = new();
    private readonly Dictionary<string, Button> _toggleButtons = new();

    // WMI refresh counter — refresh RAM/Disks every 5 seconds (10 ticks × 500ms)
    private int _wmiTickCounter = 0;
    private const int WMI_REFRESH_INTERVAL = 5;

    // Brush pooling for UsageColor/TempColor — allocated once
    private static readonly SolidColorBrush _brush85 = new(ColorFromHex("#EF4444"));
    private static readonly SolidColorBrush _brush60 = new(ColorFromHex("#F59E0B"));
    private static readonly SolidColorBrush _brushGreen = new(ColorFromHex("#4ADE80"));
    private static readonly SolidColorBrush _brushYellow = new(ColorFromHex("#FBBF24"));
    private static readonly SolidColorBrush _brushRed = new(ColorFromHex("#EF4444"));

    // Gamepad brush pooling — allocated once, reused every tick
    private static readonly SolidColorBrush GpPressedBg = new(Windows.UI.Color.FromArgb(180, 245, 158, 11));
    private static readonly SolidColorBrush GpPressedBorder = new(Windows.UI.Color.FromArgb(220, 251, 191, 36));
    private static readonly SolidColorBrush GpReleasedBg = new(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    private static readonly SolidColorBrush GpReleasedBorder = new(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    private bool[] _gpButtonStates = new bool[14]; // track previous state per button index

    // Cores por componente
    private static readonly SolidColorBrush MbColor = new(ColorFromHex("#F472B6"));
    private static readonly SolidColorBrush RamColor = new(ColorFromHex("#C084FC"));
    private static readonly SolidColorBrush CpuColor = new(ColorFromHex("#4ADE80"));
    private static readonly SolidColorBrush GpuColor = new(ColorFromHex("#60A5FA"));
    private static readonly SolidColorBrush DiskColor = new(ColorFromHex("#FBBF24"));
    private static readonly SolidColorBrush NetColor = new(ColorFromHex("#34D399"));
    private static readonly SolidColorBrush FpsColor = new(ColorFromHex("#FB923C"));
    private static readonly SolidColorBrush CardBg = Design.C.CardB;
    private static readonly SolidColorBrush CardBorder = Design.C.BorB;
    private static readonly SolidColorBrush SurfaceBg = Design.C.BgB;
    private static readonly SolidColorBrush SubtleText = Design.C.MutedB;
    private Grid? _loadingOverlay;

    public MonitorWindow()
    {
        Title = "Hardware Monitor";
        _monitor = new HardwareMonitorService();

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 960, Height = 640 });

        // Dark title bar
        var titleBar = appWindow.TitleBar;
        titleBar.ExtendsContentIntoTitleBar = false;
        var darkBg = ColorFromHex("#0A0B0D");
        var hoverBg = ColorFromHex("#2A2D31");
        var pressedBg = ColorFromHex("#1E2023");
        var mutedFg = Windows.UI.Color.FromArgb(255, 128, 128, 128);
        titleBar.BackgroundColor = darkBg;
        titleBar.ForegroundColor = Colors.White;
        titleBar.ButtonBackgroundColor = darkBg;
        titleBar.ButtonForegroundColor = Colors.White;
        titleBar.ButtonHoverBackgroundColor = hoverBg;
        titleBar.ButtonPressedBackgroundColor = pressedBg;
        titleBar.ButtonInactiveBackgroundColor = darkBg;
        titleBar.ButtonInactiveForegroundColor = mutedFg;

        // Loading overlay — shown FIRST so window appears immediately
        _loadingOverlay = new Grid { Background = SurfaceBg };
        var loadingStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 12 };
        loadingStack.Children.Add(new TextBlock { Text = "E.M.E Core", FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White), HorizontalAlignment = HorizontalAlignment.Center });
        loadingStack.Children.Add(new TextBlock { Text = "Carregando dados do sistema...", FontSize = 13, Foreground = SubtleText, HorizontalAlignment = HorizontalAlignment.Center });
        _loadingOverlay.Children.Add(loadingStack);
        Content = _loadingOverlay;

        // Defer full UI build + data load to after first frame renders
        _ = BuildAndLoadAsync();
    }

    private async Task BuildAndLoadAsync()
    {
        await Task.Delay(50);
        var root = new Grid { Background = SurfaceBg };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(232) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ===== SIDEBAR =====
        var sidebar = new Border
        {
            Background = Design.C.SideB,
            BorderThickness = new Thickness(0, 0, 1, 0), BorderBrush = CardBorder,
            Padding = new Thickness(0, 48, 0, 16)
        };
        var sidebarStack = new StackPanel { Spacing = 4, Padding = new Thickness(12) };
        var logoStack = new StackPanel { Spacing = 4, Margin = new Thickness(8, 0, 8, 24) };
        logoStack.Children.Add(new TextBlock { Text = "E.M.E Core", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) });
        logoStack.Children.Add(new TextBlock { Text = "Hardware Monitor", FontSize = 12, Foreground = SubtleText });
        sidebarStack.Children.Add(logoStack);
        _navHardware = CreateNavItem("\uE9F5", "Hardware", true, CpuColor);
        _navStressTest = CreateNavItem("\uE7F4", "Stress Test", false, SubtleText);
        _navMonitores = CreateNavItem("\uE7F4", "Monitores", false, SubtleText);
        _navPerifericos = CreateNavItem("\uE711", "Periféricos", false, SubtleText);
        sidebarStack.Children.Add(_navHardware);
        // sidebarStack.Children.Add(_navStressTest); — oculto, ideia para app futuro
        sidebarStack.Children.Add(_navMonitores);
        sidebarStack.Children.Add(_navPerifericos);
        sidebar.Child = sidebarStack;
        Grid.SetColumn(sidebar, 0);
        root.Children.Add(sidebar);

        // ===== MAIN CONTENT =====
        var contentPanel = new Grid();
        contentPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // --- Hardware Content ---
        _hardwareContent = new ScrollViewer { Padding = new Thickness(24, 48, 24, 24), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
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
        var mbHeader = CreateCardHeaderGrid("\uE950", "PLACA MÃE", MbColor, "mb");
        _mbVoltage = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(ColorFromHex("#9CA3AF")), VerticalAlignment = VerticalAlignment.Center };
        mbHeader.Children.Add(_mbVoltage);
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

        var mbCompact = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, Padding = new Thickness(0, 4, 0, 4) };
        _mbCompactTemp = new TextBlock { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = MbColor };
        _mbCompactVrm = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Colors.White) };
        _mbCompactFans = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Colors.White) };
        _mbCompactVoltage = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Colors.White) };
        mbCompact.Children.Add(new TextBlock { Text = "TEMP", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        mbCompact.Children.Add(_mbCompactTemp);
        mbCompact.Children.Add(new TextBlock { Text = "VRM", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        mbCompact.Children.Add(_mbCompactVrm);
        mbCompact.Children.Add(new TextBlock { Text = "FAN", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        mbCompact.Children.Add(_mbCompactFans);
        mbCompact.Children.Add(new TextBlock { Text = "V", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        mbCompact.Children.Add(_mbCompactVoltage);
        RegisterCard("mb", mbCard, mbStack, mbCompact);

        Grid.SetColumn(mbCard, 0);
        row1.Children.Add(mbCard);

        // RAM Card
        var ramCard = CreateCard();
        var ramStack = new StackPanel { Spacing = 12 };
        var ramHeader = CreateCardHeaderGrid("\uf515", "RAM", RamColor, "ram");
        _ramVoltage = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(ColorFromHex("#9CA3AF")), VerticalAlignment = VerticalAlignment.Center };
        ramHeader.Children.Add(_ramVoltage);
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

        var ramCompact = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, Padding = new Thickness(0, 4, 0, 4) };
        _ramCompactPct = new TextBlock { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = RamColor };
        _ramCompactInfo = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Colors.White) };
        ramCompact.Children.Add(new TextBlock { Text = "USO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        ramCompact.Children.Add(_ramCompactPct);
        ramCompact.Children.Add(new TextBlock { Text = "RAM", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        ramCompact.Children.Add(_ramCompactInfo);
        RegisterCard("ram", ramCard, ramStack, ramCompact);

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
        var cpuHeader = CreateCardHeaderGrid("\uef8e", "CPU", CpuColor, "cpu");
        _cpuVoltage = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(ColorFromHex("#9CA3AF")), VerticalAlignment = VerticalAlignment.Center };
        cpuHeader.Children.Add(_cpuVoltage);
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

        var cpuGraphContainer = new Border { Background = Design.C.InsetB, CornerRadius = new CornerRadius(8), Height = 80, Padding = new Thickness(4), BorderThickness = new Thickness(1), BorderBrush = CardBorder };
        _cpuChart = new CartesianChart
        {
            Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _cpuValues,
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 0.5,
                    Stroke = new SolidColorPaint(new SKColor(0, 210, 255), 2),
                    Fill = new SolidColorPaint(new SKColor(0, 210, 255, 30))
                }
            },
            XAxes = new Axis[]
            {
                new Axis
                {
                    IsVisible = false,
                    MinStep = 1
                }
            },
            YAxes = new Axis[]
            {
                new Axis
                {
                    IsVisible = false,
                    MinLimit = 0,
                    MaxLimit = 100
                }
            }
        };
        cpuGraphContainer.Child = _cpuChart;
        cpuStack.Children.Add(cpuGraphContainer);

        // CPU Fans
        _cpuFansPanel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 4, 0, 0) };
        cpuStack.Children.Add(_cpuFansPanel);

        var cpuCompact = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, Padding = new Thickness(0, 4, 0, 4) };
        _cpuCompactPct = new TextBlock { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = CpuColor };
        _cpuCompactCore = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Colors.White) };
        _cpuCompactPkg = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Colors.White) };
        cpuCompact.Children.Add(new TextBlock { Text = "USO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        cpuCompact.Children.Add(_cpuCompactPct);
        cpuCompact.Children.Add(new TextBlock { Text = "CORE", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        cpuCompact.Children.Add(_cpuCompactCore);
        cpuCompact.Children.Add(new TextBlock { Text = "PKG", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        cpuCompact.Children.Add(_cpuCompactPkg);
        RegisterCard("cpu", cpuCard, cpuStack, cpuCompact);

        Grid.SetColumn(cpuCard, 0);
        row2.Children.Add(cpuCard);

        // GPU Card
        var gpuCard = CreateCard();
        var gpuStack = new StackPanel { Spacing = 12 };
        var gpuHeader = CreateCardHeaderGrid("\uea89", "GPU", GpuColor, "gpu");
        _gpuVoltage = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(ColorFromHex("#9CA3AF")), VerticalAlignment = VerticalAlignment.Center };
        gpuHeader.Children.Add(_gpuVoltage);
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

        var gpuGraphContainer = new Border { Background = Design.C.InsetB, CornerRadius = new CornerRadius(8), Height = 80, Padding = new Thickness(4), BorderThickness = new Thickness(1), BorderBrush = CardBorder };
        _gpuChart = new CartesianChart
        {
            Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _gpuValues,
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 0.5,
                    Stroke = new SolidColorPaint(new SKColor(168, 85, 247), 2),
                    Fill = new SolidColorPaint(new SKColor(168, 85, 247, 30))
                }
            },
            XAxes = new Axis[]
            {
                new Axis
                {
                    IsVisible = false,
                    MinStep = 1
                }
            },
            YAxes = new Axis[]
            {
                new Axis
                {
                    IsVisible = false,
                    MinLimit = 0,
                    MaxLimit = 100
                }
            },

        };
        gpuGraphContainer.Child = _gpuChart;
        gpuStack.Children.Add(gpuGraphContainer);

        // GPU Fans
        _gpuFansPanel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 4, 0, 0) };
        gpuStack.Children.Add(_gpuFansPanel);

        var gpuCompact = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, Padding = new Thickness(0, 4, 0, 4) };
        _gpuCompactPct = new TextBlock { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = GpuColor };
        _gpuCompactTemp = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Colors.White) };
        gpuCompact.Children.Add(new TextBlock { Text = "USO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        gpuCompact.Children.Add(_gpuCompactPct);
        gpuCompact.Children.Add(new TextBlock { Text = "TEMP", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        gpuCompact.Children.Add(_gpuCompactTemp);
        RegisterCard("gpu", gpuCard, gpuStack, gpuCompact);

        Grid.SetColumn(gpuCard, 1);
        row2.Children.Add(gpuCard);
        contentStack.Children.Add(row2);

        // ===== ROW 3: Disk + Network =====
        var row3 = new Grid { ColumnSpacing = 16 };
        row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row3.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Disk Card — dynamic, supports multiple disks
        var diskCard = CreateCard();
        var diskStack = new StackPanel { Spacing = 12 };
        diskStack.Children.Add(CreateCardHeaderGrid("\uEDA2", "DISCO", DiskColor, "disk"));
        _disksPanel = new StackPanel { Spacing = 8 };
        diskStack.Children.Add(_disksPanel);

        var diskCompact = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, Padding = new Thickness(0, 4, 0, 4) };
        _diskCompactInfo = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Colors.White) };
        diskCompact.Children.Add(new TextBlock { Text = "ESPAÇO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        diskCompact.Children.Add(_diskCompactInfo);
        RegisterCard("disk", diskCard, diskStack, diskCompact);

        Grid.SetColumn(diskCard, 0);
        row3.Children.Add(diskCard);

        // Network Card
        var netCard = CreateCard();
        var netStack = new StackPanel { Spacing = 12 };
        netStack.Children.Add(CreateCardHeaderGrid("\uE8B0", "REDE", NetColor, "net"));
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
        var netGraphGrid = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 4, 0, 0), Height = 80 };
        netGraphGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        netGraphGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var downGraphBg = new Border { Background = Design.C.InsetB, CornerRadius = new CornerRadius(6), Padding = new Thickness(4), BorderThickness = new Thickness(1), BorderBrush = CardBorder };

        _netDownChart = new CartesianChart
        {
            Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _netDownValues,
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 0.5,
                    Stroke = new SolidColorPaint(new SKColor(76, 203, 160), 2),
                    Fill = new SolidColorPaint(new SKColor(76, 203, 160, 30))
                }
            },
            XAxes = new Axis[] { new Axis { IsVisible = false, MinStep = 1 } },
            YAxes = new Axis[] { new Axis { IsVisible = false, MinLimit = 0 } },

        };
        downGraphBg.Child = _netDownChart;
        netGraphGrid.Children.Add(downGraphBg);
        var upGraphBg = new Border { Background = Design.C.InsetB, CornerRadius = new CornerRadius(6), Padding = new Thickness(4), BorderThickness = new Thickness(1), BorderBrush = CardBorder };

        _netUpChart = new CartesianChart
        {
            Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _netUpValues,
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 0.5,
                    Stroke = new SolidColorPaint(SKColors.White, 2),
                    Fill = new SolidColorPaint(new SKColor(255, 255, 255, 20))
                }
            },
            XAxes = new Axis[] { new Axis { IsVisible = false, MinStep = 1 } },
            YAxes = new Axis[] { new Axis { IsVisible = false, MinLimit = 0 } },

        };
        upGraphBg.Child = _netUpChart;
        Grid.SetColumn(upGraphBg, 1);
        netGraphGrid.Children.Add(upGraphBg);

        netStack.Children.Add(netGraphGrid);

        var netCompact = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, Padding = new Thickness(0, 4, 0, 4) };
        _netCompactDown = new TextBlock { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = NetColor };
        _netCompactUp = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Colors.White) };
        netCompact.Children.Add(new TextBlock { Text = "↓", FontSize = 12, Foreground = NetColor, VerticalAlignment = VerticalAlignment.Center });
        netCompact.Children.Add(_netCompactDown);
        netCompact.Children.Add(new TextBlock { Text = "↑", FontSize = 12, Foreground = new SolidColorBrush(Colors.White), VerticalAlignment = VerticalAlignment.Center });
        netCompact.Children.Add(_netCompactUp);
        RegisterCard("net", netCard, netStack, netCompact);

        Grid.SetColumn(netCard, 1);
        row3.Children.Add(netCard);
        contentStack.Children.Add(row3);

        // ===== ROW 4: FPS =====
        _fpsCard = CreateCard();
        var fpsStack = new StackPanel { Spacing = 12 };

        var fpsHeaderGrid = new Grid();
        fpsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fpsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var fpsHdr = CreateCardHeader("\uec4d", "FPS", FpsColor);
        fpsHeaderGrid.Children.Add(fpsHdr);

        _fpsToggleBtn = new Button
        {
            Content = "INICIAR",
            FontSize = 10,
            Foreground = FpsColor,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(1),
            BorderBrush = FpsColor,
            Padding = new Thickness(10, 4, 10, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        _fpsToggleBtn.Click += FpsToggle_Click;
        Grid.SetColumn(_fpsToggleBtn, 1);
        fpsHeaderGrid.Children.Add(_fpsToggleBtn);
        fpsStack.Children.Add(fpsHeaderGrid);
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

        _hardwareContent.Content = contentStack;

        // --- Stress Test Content ---
        _stressTestContent = new ScrollViewer { Padding = new Thickness(24, 48, 24, 24), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var stressStack = new StackPanel { Spacing = 16 };

        var stressHeader = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        stressHeader.Children.Add(new TextBlock { Text = "Stress Test", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) });
        stressHeader.Children.Add(new TextBlock { Text = "Teste a estabilidade do seu hardware", FontSize = 13, Foreground = SubtleText });
        stressStack.Children.Add(stressHeader);

        // Stress Test Cards Row (side by side)
        var stressRow = new Grid { ColumnSpacing = 16 };
        stressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        stressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // CPU Stress Test Card
        var cpuStressCard = CreateCard();
        var cpuStressStack = new StackPanel { Spacing = 12 };
        var cpuStressHdr = CreateCardHeaderGrid("\uef8e", "CPU STRESS TEST", CpuColor, "stress_cpu");
        _cpuStressIndicator = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = SubtleText, VerticalAlignment = VerticalAlignment.Center };
        cpuStressHdr.Children.Add(_cpuStressIndicator);
        cpuStressStack.Children.Add(cpuStressHdr);

        _cpuStressModel = new TextBlock { Text = "CPU", FontSize = 11, Foreground = SubtleText, TextTrimming = TextTrimming.CharacterEllipsis };
        cpuStressStack.Children.Add(_cpuStressModel);

        _cpuStressStatus = new TextBlock { Text = "Parado", FontSize = 14, Foreground = SubtleText };
        cpuStressStack.Children.Add(_cpuStressStatus);

        var cpuStressMetrics = new Grid { ColumnSpacing = 24 };
        cpuStressMetrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cpuStressMetrics.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var cpuTimeStack = new StackPanel { Spacing = 2 };
        cpuTimeStack.Children.Add(new TextBlock { Text = "DURAÇÃO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _cpuStressTime = new TextBlock { Text = "00:00:00", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) };
        cpuTimeStack.Children.Add(_cpuStressTime);
        cpuStressMetrics.Children.Add(cpuTimeStack);

        var cpuLoadStack = new StackPanel { Spacing = 2 };
        cpuLoadStack.Children.Add(new TextBlock { Text = "LOAD", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _cpuStressLoad = new TextBlock { Text = "0%", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = CpuColor };
        cpuLoadStack.Children.Add(_cpuStressLoad);
        Grid.SetColumn(cpuLoadStack, 1);
        cpuStressMetrics.Children.Add(cpuLoadStack);

        cpuStressStack.Children.Add(cpuStressMetrics);

        _cpuStressBtn = new Button
        {
            Content = "Iniciar CPU Stress",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 40,
            CornerRadius = new CornerRadius(8),
            Background = CpuColor,
            Foreground = new SolidColorBrush(Colors.Black),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        _cpuStressBtn.Click += CpuStressBtn_Click;
        cpuStressStack.Children.Add(_cpuStressBtn);

        // CPU Temperature Graph
        var cpuTempGraphLabel = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        cpuTempGraphLabel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cpuTempGraphLabel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cpuTempGraphLabel.Children.Add(new TextBlock { Text = "TEMPERATURA", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _cpuStressTempValue = new TextBlock { Text = "--°C", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = CpuColor };
        Grid.SetColumn(_cpuStressTempValue, 1);
        cpuTempGraphLabel.Children.Add(_cpuStressTempValue);
        cpuStressStack.Children.Add(cpuTempGraphLabel);

        var cpuStressTempGraphBg = new Border { Background = Design.C.InsetB, CornerRadius = new CornerRadius(6), Padding = new Thickness(4), Height = 80, BorderThickness = new Thickness(1), BorderBrush = CardBorder };
        _cpuStressTempChart = new CartesianChart
        {
            Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _cpuStressTempValues,
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 0.5,
                    Stroke = new SolidColorPaint(new SKColor(0, 210, 255), 2),
                    Fill = new SolidColorPaint(new SKColor(0, 210, 255, 30))
                }
            },
            XAxes = new Axis[] { new Axis { IsVisible = false, MinStep = 1 } },
            YAxes = new Axis[] { new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 100 } },

        };
        cpuStressTempGraphBg.Child = _cpuStressTempChart;
        cpuStressStack.Children.Add(cpuStressTempGraphBg);

        // CPU Usage Graph
        var cpuUsageGraphLabel = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        cpuUsageGraphLabel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cpuUsageGraphLabel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cpuUsageGraphLabel.Children.Add(new TextBlock { Text = "USO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _cpuStressLoadValue = new TextBlock { Text = "0%", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = CpuColor };
        Grid.SetColumn(_cpuStressLoadValue, 1);
        cpuUsageGraphLabel.Children.Add(_cpuStressLoadValue);
        cpuStressStack.Children.Add(cpuUsageGraphLabel);

        var cpuStressUsageGraphBg = new Border { Background = Design.C.InsetB, CornerRadius = new CornerRadius(6), Padding = new Thickness(4), Height = 80, BorderThickness = new Thickness(1), BorderBrush = CardBorder };
        _cpuStressUsageChart = new CartesianChart
        {
            Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _cpuStressUsageValues,
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 0.5,
                    Stroke = new SolidColorPaint(new SKColor(239, 68, 68), 2),
                    Fill = new SolidColorPaint(new SKColor(239, 68, 68, 30))
                }
            },
            XAxes = new Axis[] { new Axis { IsVisible = false, MinStep = 1 } },
            YAxes = new Axis[] { new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 100 } },

        };
        cpuStressUsageGraphBg.Child = _cpuStressUsageChart;
        cpuStressStack.Children.Add(cpuStressUsageGraphBg);

        cpuStressCard.Child = cpuStressStack;
        Grid.SetColumn(cpuStressCard, 0);
        stressRow.Children.Add(cpuStressCard);

        // GPU Monitor Card (read-only)
        var gpuStressCard = CreateCard();
        var gpuStressStack = new StackPanel { Spacing = 12 };
        var gpuStressHdr = CreateCardHeaderGrid("\uea89", "GPU MONITOR", GpuColor, "stress_gpu");
        _gpuStressIndicator = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = SubtleText, VerticalAlignment = VerticalAlignment.Center };
        gpuStressHdr.Children.Add(_gpuStressIndicator);
        gpuStressStack.Children.Add(gpuStressHdr);

        _gpuStressModel = new TextBlock { Text = "GPU", FontSize = 11, Foreground = SubtleText, TextTrimming = TextTrimming.CharacterEllipsis };
        gpuStressStack.Children.Add(_gpuStressModel);

        _gpuStressStatus = new TextBlock { Text = "Monitoramento em tempo real", FontSize = 14, Foreground = SubtleText };
        gpuStressStack.Children.Add(_gpuStressStatus);

        var gpuStressMetrics = new Grid { ColumnSpacing = 24 };
        gpuStressMetrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gpuStressMetrics.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var gpuSTTempStack = new StackPanel { Spacing = 2 };
        gpuSTTempStack.Children.Add(new TextBlock { Text = "TEMPERATURA", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _gpuStressTemp = new TextBlock { Text = "--°C", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) };
        gpuSTTempStack.Children.Add(_gpuStressTemp);
        gpuStressMetrics.Children.Add(gpuSTTempStack);

        var gpuSTUsageStack = new StackPanel { Spacing = 2 };
        gpuSTUsageStack.Children.Add(new TextBlock { Text = "USO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _gpuStressUsage = new TextBlock { Text = "0%", FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = GpuColor };
        gpuSTUsageStack.Children.Add(_gpuStressUsage);
        Grid.SetColumn(gpuSTUsageStack, 1);
        gpuStressMetrics.Children.Add(gpuSTUsageStack);

        gpuStressStack.Children.Add(gpuStressMetrics);

        // FurMark status
        _furmarkStatus = new TextBlock { Text = "", FontSize = 11, Foreground = SubtleText };
        gpuStressStack.Children.Add(_furmarkStatus);

        _gpuStressBtn = new Button
        {
            Content = "Iniciar GPU Stress",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 40,
            CornerRadius = new CornerRadius(8),
            Background = GpuColor,
            Foreground = new SolidColorBrush(Colors.Black),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        _gpuStressBtn.Click += GpuStressBtn_Click;
        gpuStressStack.Children.Add(_gpuStressBtn);

        // GPU Temperature Graph
        var gpuTempGraphLabel = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        gpuTempGraphLabel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gpuTempGraphLabel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        gpuTempGraphLabel.Children.Add(new TextBlock { Text = "TEMPERATURA", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _gpuStressTempValue = new TextBlock { Text = "--°C", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = GpuColor };
        Grid.SetColumn(_gpuStressTempValue, 1);
        gpuTempGraphLabel.Children.Add(_gpuStressTempValue);
        gpuStressStack.Children.Add(gpuTempGraphLabel);

        var gpuStressTempGraphBg = new Border { Background = Design.C.InsetB, CornerRadius = new CornerRadius(6), Padding = new Thickness(4), Height = 80, BorderThickness = new Thickness(1), BorderBrush = CardBorder };
        _gpuStressTempChart = new CartesianChart
        {
            Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _gpuStressTempValues,
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 0.5,
                    Stroke = new SolidColorPaint(new SKColor(168, 85, 247), 2),
                    Fill = new SolidColorPaint(new SKColor(168, 85, 247, 30))
                }
            },
            XAxes = new Axis[] { new Axis { IsVisible = false, MinStep = 1 } },
            YAxes = new Axis[] { new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 100 } },

        };
        gpuStressTempGraphBg.Child = _gpuStressTempChart;
        gpuStressStack.Children.Add(gpuStressTempGraphBg);

        // GPU Usage Graph
        var gpuUsageGraphLabel = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        gpuUsageGraphLabel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        gpuUsageGraphLabel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        gpuUsageGraphLabel.Children.Add(new TextBlock { Text = "USO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _gpuStressLoadValue = new TextBlock { Text = "0%", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = GpuColor };
        Grid.SetColumn(_gpuStressLoadValue, 1);
        gpuUsageGraphLabel.Children.Add(_gpuStressLoadValue);
        gpuStressStack.Children.Add(gpuUsageGraphLabel);

        var gpuStressUsageGraphBg = new Border { Background = Design.C.InsetB, CornerRadius = new CornerRadius(6), Padding = new Thickness(4), Height = 80, BorderThickness = new Thickness(1), BorderBrush = CardBorder };
        _gpuStressUsageChart = new CartesianChart
        {
            Series = new ISeries[]
            {
                new LineSeries<double>
                {
                    Values = _gpuStressUsageValues,
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 0.5,
                    Stroke = new SolidColorPaint(new SKColor(239, 68, 68), 2),
                    Fill = new SolidColorPaint(new SKColor(239, 68, 68, 30))
                }
            },
            XAxes = new Axis[] { new Axis { IsVisible = false, MinStep = 1 } },
            YAxes = new Axis[] { new Axis { IsVisible = false, MinLimit = 0, MaxLimit = 100 } },

        };
        gpuStressUsageGraphBg.Child = _gpuStressUsageChart;
        gpuStressStack.Children.Add(gpuStressUsageGraphBg);

        gpuStressCard.Child = gpuStressStack;
        Grid.SetColumn(gpuStressCard, 1);
        stressRow.Children.Add(gpuStressCard);

        stressStack.Children.Add(stressRow);
        _stressTestContent.Content = stressStack;

        // --- Monitores Content ---
        _monitoresContent = new ScrollViewer { Padding = new Thickness(24, 48, 24, 24), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var monitoresStack = new StackPanel { Spacing = 16 };

        var monitoresHeader = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        monitoresHeader.Children.Add(new TextBlock { Text = "Monitores", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) });
        monitoresHeader.Children.Add(new TextBlock { Text = "Informações dos monitores conectados", FontSize = 13, Foreground = SubtleText });
        monitoresStack.Children.Add(monitoresHeader);

        _monitoresContent.Content = monitoresStack;

        // --- Periféricos Content ---
        _perifericosContent = new ScrollViewer { Padding = new Thickness(24, 48, 24, 24), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var perifericosStack = new StackPanel { Spacing = 16 };

        var perifericosHeader = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        perifericosHeader.Children.Add(new TextBlock { Text = "Periféricos", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) });
        perifericosHeader.Children.Add(new TextBlock { Text = "Controles e dispositivos conectados", FontSize = 13, Foreground = SubtleText });
        perifericosStack.Children.Add(perifericosHeader);

        _perifericosContent.Content = perifericosStack;

        // Add all content panels to the grid
        contentPanel.Children.Add(_hardwareContent);
        contentPanel.Children.Add(_stressTestContent);
        contentPanel.Children.Add(_monitoresContent);
        contentPanel.Children.Add(_perifericosContent);
        _stressTestContent.Visibility = Visibility.Collapsed;
        _monitoresContent.Visibility = Visibility.Collapsed;
        _perifericosContent.Visibility = Visibility.Collapsed;

        Grid.SetColumn(contentPanel, 1);
        root.Children.Add(contentPanel);

        // Loading overlay — spans full window
        _loadingOverlay = new Grid { Background = SurfaceBg, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        var loadingStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 12 };
        loadingStack.Children.Add(new TextBlock { Text = "E.M.E Core", FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White), HorizontalAlignment = HorizontalAlignment.Center });
        loadingStack.Children.Add(new TextBlock { Text = "Carregando dados do sistema...", FontSize = 13, Foreground = SubtleText, HorizontalAlignment = HorizontalAlignment.Center });
        _loadingOverlay.Children.Add(loadingStack);
        Grid.SetColumnSpan(_loadingOverlay, 2);
        root.Children.Add(_loadingOverlay);

        Content = root;

        // Navigation click handlers
        _navHardware.Click += (_, _) => SwitchTab("hardware");
        _navStressTest.Click += (_, _) => SwitchTab("stresstest");
        _navMonitores.Click += (_, _) => SwitchTab("monitores");
        _navPerifericos.Click += (_, _) => SwitchTab("perifericos");

        Closed += (_, _) => { _bgTimer?.Dispose(); _graphTimer.Stop(); _stressTimer.Stop(); _gamepadTimer.Stop(); _stressTest.Dispose(); _monitor.Dispose(); };
        var hwnd2 = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId2 = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd2);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId2);
        appWindow.Changed += (_, args) =>
        {
            if (args.DidPositionChange) { if (!_isMoving) { _isMoving = true; _bgTimer?.Change(Timeout.Infinite, Timeout.Infinite); _graphTimer.Stop(); } }
            else if (_isMoving) { _isMoving = false; _bgTimer?.Change(1000, 1000); _graphTimer.Start(); }
        };

        _bgTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                var s = _monitor.CollectFast();
                DispatcherQueue.TryEnqueue(() => RefreshLhmWith(s));
            }
            catch { }
        }, null, 1000, 1000);

        _graphTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
        _graphTimer.Tick += (_, _) => UpdateGraphs();

        _stressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _stressTimer.Tick += (_, _) => UpdateStressTestMetrics();

        // Gamepad real-time timer (16ms = ~60 Hz)
        _gamepadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _gamepadTimer.Tick += (_, _) => UpdateGamepadState();

        // Detect FurMark on startup
        if (_stressTest.DetectFurMark())
        {
            _furmarkStatus.Text = $"FurMark: {_stressTest.FurMarkPath}";
            _furmarkStatus.Foreground = new SolidColorBrush(ColorFromHex("#4ADE80"));
        }
        else
        {
            _furmarkStatus.Text = "FurMark não encontrado. Instale o FurMark 2 para GPU Stress.";
            _furmarkStatus.Foreground = SteamColors.RedBrush;
            _gpuStressBtn.IsEnabled = false;
        }

        // PHASE 1: LHM fast data (~4ms) — populate CPU/GPU/MB/Fans immediately
        RefreshLhm();

        //_timer.Start();
        _graphTimer.Start();
        // _stressTimer.Start(); — oculto com a aba Stress Test

        if (_loadingOverlay != null)
            _loadingOverlay.Visibility = Visibility.Collapsed;

        // PHASE 2: WMI slow data (RAM/Disk/Network/Monitors) — background
        _ = LoadWmiInBackgroundAsync();
    }

    private bool _wmiLoading;

    private async Task LoadWmiInBackgroundAsync()
    {
        if (_wmiLoading) return;
        _wmiLoading = true;
        try
        {
            var s = await Task.Run(() => _monitor.Collect());
            if (s == null) return;

            DispatcherQueue.TryEnqueue(() =>
            {
                _lastCollect = s;
                RefreshWmi(s);
            });
        }
        finally
        {
            _wmiLoading = false;
        }
    }

    private static Border CreateCard() => new()
    {
        Background = CardBg, CornerRadius = Design.R.XXL,
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

    private StackPanel CreateCardHeaderGrid(string icon, string title, SolidColorBrush accentColor, string cardKey)
    {
        var hdr = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        hdr.Children.Add(new FontIcon { Glyph = icon, FontSize = 18, Foreground = accentColor, FontFamily = new FontFamily("Assets/tabler-icons.ttf#tabler-icons") });
        hdr.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White), VerticalAlignment = VerticalAlignment.Center });

        return hdr;
    }

    private bool _fpsRunning;

    private void FpsToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_fpsRunning)
        {
            _monitor.StopFpsMonitor();
            _fpsRunning = false;
            _fpsToggleBtn.Content = "INICIAR";
            _fpsToggleBtn.Foreground = FpsColor;
            _fpsToggleBtn.BorderBrush = FpsColor;
        }
        else
        {
            _monitor.StartFpsMonitor("");
            _fpsRunning = true;
            _fpsToggleBtn.Content = "PARAR";
            _fpsToggleBtn.Foreground = _brushRed;
            _fpsToggleBtn.BorderBrush = _brushRed;
        }
    }

    private void CardToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string cardKey) return;

        _collapsedState[cardKey] = !_collapsedState.GetValueOrDefault(cardKey, false);
        var isCollapsed = _collapsedState[cardKey];

        btn.Content = isCollapsed ? "▲" : "▼";

        if (_expandedContent.TryGetValue(cardKey, out var expanded))
            expanded.Visibility = isCollapsed ? Visibility.Collapsed : Visibility.Visible;
        if (_collapsedContent.TryGetValue(cardKey, out var collapsed))
            collapsed.Visibility = isCollapsed ? Visibility.Visible : Visibility.Collapsed;
    }

    private StackPanel CreateCompactRow(params (string label, TextBlock value, SolidColorBrush color)[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, Padding = new Thickness(0, 2, 0, 2) };
        foreach (var (label, value, color) in items)
        {
            var itemStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            itemStack.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
            var valClone = new TextBlock { Text = value.Text, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = color, VerticalAlignment = VerticalAlignment.Center, Tag = value.Tag };
            value.Tag = valClone;
            itemStack.Children.Add(valClone);
            row.Children.Add(itemStack);
        }
        return row;
    }

    private void RegisterCard(string key, Border card, StackPanel expanded, StackPanel collapsed, string toggleGlyph = "▼")
    {
        _collapsedState[key] = false;
        _expandedContent[key] = expanded;
        _collapsedContent[key] = collapsed;
        _cardBorders[key] = card;
        collapsed.Visibility = Visibility.Collapsed;

        var header = expanded.Children.Count > 0 ? expanded.Children[0] as UIElement : null;
        if (header != null)
            expanded.Children.RemoveAt(0);

        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (header != null)
            headerRow.Children.Add(header);

        var toggleBtn = new Button
        {
            Content = toggleGlyph,
            FontSize = 12,
            Foreground = SubtleText,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = key
        };
        toggleBtn.Click += CardToggle_Click;
        _toggleButtons[key] = toggleBtn;
        Grid.SetColumn(toggleBtn, 1);
        headerRow.Children.Add(toggleBtn);

        var container = new StackPanel { Spacing = 12, VerticalAlignment = VerticalAlignment.Top };
        container.Children.Add(headerRow);
        container.Children.Add(expanded);
        container.Children.Add(collapsed);
        card.Child = container;
        card.VerticalAlignment = VerticalAlignment.Top;
    }

    private static Grid CreateBar(out Grid bar, SolidColorBrush color)
    {
        var fill = new Border { Background = color, CornerRadius = new CornerRadius(4), Height = 6 };
        var bg = new Border { Background = Design.C.SecB, CornerRadius = new CornerRadius(4), Height = 6 };
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
        panel.Children.Add(new FontIcon { Glyph = icon, FontSize = 16, Foreground = isActive ? accentColor : Design.C.MutedB, FontFamily = new FontFamily("Assets/tabler-icons.ttf#tabler-icons") });
        panel.Children.Add(new TextBlock { Text = label, FontSize = 13, FontWeight = isActive ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center });
        btn.Content = panel;
        return btn;
    }

    private void RefreshLhm()
    {
        var s = _monitor.CollectFast();
        _lastCollect = s;
        RefreshLhmWith(s);
    }

    private void RefreshLhmWith(HardwareStats s)
    {
        try
        {

            // Motherboard (LHM)
            _mbModel.Text = s.MotherboardModel;
            _mbTemp.Text = s.MotherboardTemp > 0 ? $"{s.MotherboardTemp:F0}°C" : "--";
            _mbTemp.Foreground = TempColor(s.MotherboardTemp);
            _mbVrmTemp.Text = s.MotherboardVrmTemp > 0 ? $"{s.MotherboardVrmTemp:F0}°C" : "--";
            _mbVrmTemp.Foreground = TempColor(s.MotherboardVrmTemp);
            _mbVoltage.Text = s.MotherboardVoltage > 0 ? $"{s.MotherboardVoltage:F2}V" : "";
            _mbCompactTemp.Text = s.MotherboardTemp > 0 ? $"{s.MotherboardTemp:F0}°C" : "--";
            _mbCompactVrm.Text = s.MotherboardVrmTemp > 0 ? $"{s.MotherboardVrmTemp:F0}°C" : "--";
            _mbCompactFans.Text = s.Fans.Count > 0 ? $"{s.Fans[0].Rpm:F0} RPM" : "--";
            _mbCompactVoltage.Text = s.MotherboardVoltage > 0 ? $"{s.MotherboardVoltage:F2}V" : "--";

            // CPU (LHM)
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
            _cpuValues.Add(s.CpuUsage);
            if (_cpuValues.Count > 60) _cpuValues.RemoveAt(0);
            _cpuCompactPct.Text = $"{s.CpuUsage:F0}%";
            _cpuCompactCore.Text = s.CpuTemp > 0 ? $"{s.CpuTemp:F0}°C" : "--";
            _cpuCompactPkg.Text = s.CpuPackageTemp > 0 ? $"{s.CpuPackageTemp:F0}°C" : "--";

            // GPU (LHM)
            _gpuModel.Text = s.GpuModel;
            var gpuPowerStr = s.GpuVoltage > 0 ? $"{s.GpuVoltage:F3}V" : "";
            if (s.GpuPower > 0) gpuPowerStr += $" | {s.GpuPower:F0}W";
            _gpuVoltage.Text = gpuPowerStr;
            _gpuPct.Text = $"{s.GpuUsage:F0}%";
            _gpuPct.Foreground = UsageColor(s.GpuUsage, GpuColor);
            SetBar(_gpuBar, s.GpuUsage, UsageColor(s.GpuUsage, GpuColor));
            _gpuCoreTemp.Text = s.GpuTemp > 0 ? $"{s.GpuTemp:F0}°C" : "--";
            _gpuCoreTemp.Foreground = TempColor(s.GpuTemp);
            _gpuValues.Add(s.GpuUsage);
            if (_gpuValues.Count > 60) _gpuValues.RemoveAt(0);
            _gpuCompactPct.Text = $"{s.GpuUsage:F0}%";
            _gpuCompactTemp.Text = s.GpuTemp > 0 ? $"{s.GpuTemp:F0}°C" : "--";

            // Fans
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
                    var category = _mapping.GetFanCategory(f.Name);
                    switch (category)
                    {
                        case "CPU": cpuFans.Add(f); break;
                        case "GPU": gpuFans.Add(f); break;
                        default: mbFans.Add(f); break;
                    }
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

            // Periodic WMI refresh for RAM/Disks (every 5 seconds)
            _wmiTickCounter++;
            if (_wmiTickCounter >= WMI_REFRESH_INTERVAL)
            {
                _wmiTickCounter = 0;
                _ = LoadWmiInBackgroundAsync();
            }
        }
        catch { }
    }

    private void RefreshWmi(HardwareStats s)
    {
        try
        {
            // RAM
            var rp = s.TotalRam > 0 ? s.UsedRam / s.TotalRam * 100 : 0;
            _ramModel.Text = s.RamModuleCount > 0 ? $"{s.RamModuleCount}x{FmtSize(s.RamModuleSize)} {s.RamModel} @ {s.RamSpeed} MHz" : $"{s.RamModel} @ {s.RamSpeed} MHz";
            _ramVoltage.Text = s.RamVoltage > 0 ? $"{s.RamVoltage:F1}V" : "";
            _ramInfo.Text = $"{s.UsedRam:F1} / {s.TotalRam:F1} GB";
            _ramPct.Text = $"{rp:F0}%";
            _ramPct.Foreground = UsageColor(rp, RamColor);
            SetBar(_ramBar, rp, UsageColor(rp, RamColor));
            _ramCompactPct.Text = $"{rp:F0}%";
            _ramCompactInfo.Text = $"{s.UsedRam:F1} / {s.TotalRam:F1} GB";

            // Disks (dynamic)
            PopulateDisks(s.Disks);

            // Network
            _netName.Text = s.NetworkName;
            var netDown = s.NetworkDownloadSpeed > 0 ? s.NetworkDownloadSpeed : _lastNetDown;
            var netUp = s.NetworkUploadSpeed > 0 ? s.NetworkUploadSpeed : _lastNetUp;
            _lastNetDown = netDown;
            _lastNetUp = netUp;
            _netDown.Text = netDown > 0 ? FormatSpeed(netDown) : "--";
            _netUp.Text = netUp > 0 ? FormatSpeed(netUp) : "--";
            _netDownValues.Add(netDown);
            _netUpValues.Add(netUp);
            if (_netDownValues.Count > 60) _netDownValues.RemoveAt(0);
            if (_netUpValues.Count > 60) _netUpValues.RemoveAt(0);
            _netCompactDown.Text = netDown > 0 ? FormatSpeed(netDown) : "--";
            _netCompactUp.Text = netUp > 0 ? FormatSpeed(netUp) : "--";

            // GPU fallback from nvidia-smi
            if (s.GpuUsage > 0 && _gpuPct.Text == "--%")
            {
                _gpuPct.Text = $"{s.GpuUsage:F0}%";
                _gpuPct.Foreground = UsageColor(s.GpuUsage, GpuColor);
                SetBar(_gpuBar, s.GpuUsage, UsageColor(s.GpuUsage, GpuColor));
                _gpuCoreTemp.Text = s.GpuTemp > 0 ? $"{s.GpuTemp:F0}°C" : "--";
                _gpuCoreTemp.Foreground = TempColor(s.GpuTemp);
            }
        }
        catch { }
    }

    private void PopulateDisks(List<DiskInfo> disks)
    {
        if (disks.Count == 0) return;

        _disksPanel.Children.Clear();

        foreach (var disk in disks)
        {
            var diskBorder = new Border
            {
                Background = Design.C.InsetB,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                BorderThickness = new Thickness(1),
                BorderBrush = CardBorder
            };
            var diskGrid = new Grid();
            diskGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            diskGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            diskGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Row 0: Name + DriveType badge
            var headerRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var diskTypeBadge = new Border
            {
                Background = disk.DriveType == 2 ? new SolidColorBrush(ColorFromHex("#7C3AED")) : new SolidColorBrush(ColorFromHex("#1E3A5F")),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            var typeText = new TextBlock
            {
                Text = disk.DriveType == 2 ? "USB" : disk.FileSystem,
                FontSize = 9,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center
            };
            diskTypeBadge.Child = typeText;
            Grid.SetColumn(diskTypeBadge, 1);
            headerRow.Children.Add(diskTypeBadge);

            var nameLabel = new TextBlock
            {
                Text = $"{disk.DeviceId} — {disk.Model}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            headerRow.Children.Add(nameLabel);
            Grid.SetRow(headerRow, 0);
            diskGrid.Children.Add(headerRow);

            // Row 1: Usage bar — star-based proportional
            var barGrid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(disk.UsagePercent, GridUnitType.Star) });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - disk.UsagePercent, GridUnitType.Star) });

            var barFill = new Border
            {
                CornerRadius = new CornerRadius(3),
                Height = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = UsageColor(disk.UsagePercent, DiskColor)
            };
            Grid.SetColumn(barFill, 0);
            barGrid.Children.Add(barFill);

            var barEmpty = new Border
            {
                CornerRadius = new CornerRadius(3),
                Height = 6,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = Design.C.SecB
            };
            Grid.SetColumn(barEmpty, 1);
            barGrid.Children.Add(barEmpty);

            Grid.SetRow(barGrid, 1);
            diskGrid.Children.Add(barGrid);

            // Row 2: Stats
            var statsGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var pctText = new TextBlock
            {
                Text = $"{disk.UsagePercent:F0}%",
                FontSize = 18,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = UsageColor(disk.UsagePercent, DiskColor),
                VerticalAlignment = VerticalAlignment.Center
            };
            statsGrid.Children.Add(pctText);

            var detailStack = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
            detailStack.Children.Add(new TextBlock
            {
                Text = $"{disk.UsedGb:F1} / {disk.TotalGb:F1} GB",
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Right
            });

            var hasSpeed = disk.ReadKbps > 0 || disk.WriteKbps > 0;
            if (hasSpeed)
            {
                detailStack.Children.Add(new TextBlock
                {
                    Text = $"↓ {FormatSpeed(disk.ReadKbps)}  ↑ {FormatSpeed(disk.WriteKbps)}",
                    FontSize = 10,
                    Foreground = SubtleText,
                    HorizontalAlignment = HorizontalAlignment.Right
                });
            }
            Grid.SetColumn(detailStack, 1);
            statsGrid.Children.Add(detailStack);
            Grid.SetRow(statsGrid, 2);
            diskGrid.Children.Add(statsGrid);

            diskBorder.Child = diskGrid;
            _disksPanel.Children.Add(diskBorder);
        }

        if (disks.Count > 0)
        {
            var firstDisk = disks[0];
            _diskCompactInfo.Text = $"{firstDisk.UsagePercent:F0}% | {firstDisk.UsedGb:F1}/{firstDisk.TotalGb:F1} GB";
        }
    }

    private void Refresh()
    {
        try
        {
            var s = _monitor.Collect();
            _lastCollect = s;

            // Motherboard
            _mbModel.Text = s.MotherboardModel;
            _mbTemp.Text = s.MotherboardTemp > 0 ? $"{s.MotherboardTemp:F0}°C" : "--";
            _mbTemp.Foreground = TempColor(s.MotherboardTemp);
            _mbVrmTemp.Text = s.MotherboardVrmTemp > 0 ? $"{s.MotherboardVrmTemp:F0}°C" : "--";
            _mbVrmTemp.Foreground = TempColor(s.MotherboardVrmTemp);
            _mbVoltage.Text = s.MotherboardVoltage > 0 ? $"{s.MotherboardVoltage:F2}V" : "";
            _mbCompactTemp.Text = s.MotherboardTemp > 0 ? $"{s.MotherboardTemp:F0}°C" : "--";
            _mbCompactVrm.Text = s.MotherboardVrmTemp > 0 ? $"{s.MotherboardVrmTemp:F0}°C" : "--";
            _mbCompactFans.Text = s.Fans.Count > 0 ? $"{s.Fans[0].Rpm:F0} RPM" : "--";
            _mbCompactVoltage.Text = s.MotherboardVoltage > 0 ? $"{s.MotherboardVoltage:F2}V" : "--";

            // RAM
            var rp = s.TotalRam > 0 ? s.UsedRam / s.TotalRam * 100 : 0;
            _ramModel.Text = s.RamModuleCount > 0 ? $"{s.RamModuleCount}x{FmtSize(s.RamModuleSize)} {s.RamModel} @ {s.RamSpeed} MHz" : $"{s.RamModel} @ {s.RamSpeed} MHz";
            _ramVoltage.Text = s.RamVoltage > 0 ? $"{s.RamVoltage:F1}V" : "";
            _ramInfo.Text = $"{s.UsedRam:F1} / {s.TotalRam:F1} GB";
            _ramPct.Text = $"{rp:F0}%";
            _ramPct.Foreground = UsageColor(rp, RamColor);
            SetBar(_ramBar, rp, UsageColor(rp, RamColor));
            _ramCompactPct.Text = $"{rp:F0}%";
            _ramCompactInfo.Text = $"{s.UsedRam:F1} / {s.TotalRam:F1} GB";

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
            _cpuValues.Add(s.CpuUsage);
            if (_cpuValues.Count > 60) _cpuValues.RemoveAt(0);
            _cpuCompactPct.Text = $"{s.CpuUsage:F0}%";
            _cpuCompactCore.Text = s.CpuTemp > 0 ? $"{s.CpuTemp:F0}°C" : "--";
            _cpuCompactPkg.Text = s.CpuPackageTemp > 0 ? $"{s.CpuPackageTemp:F0}°C" : "--";

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
            _gpuValues.Add(s.GpuUsage);
            if (_gpuValues.Count > 60) _gpuValues.RemoveAt(0);
            _gpuCompactPct.Text = $"{s.GpuUsage:F0}%";
            _gpuCompactTemp.Text = s.GpuTemp > 0 ? $"{s.GpuTemp:F0}°C" : "--";

            // Disks
            PopulateDisks(s.Disks);

            // Network (with anti-flicker - cache last value)
            _netName.Text = s.NetworkName;
            var netDown = s.NetworkDownloadSpeed > 0 ? s.NetworkDownloadSpeed : _lastNetDown;
            var netUp = s.NetworkUploadSpeed > 0 ? s.NetworkUploadSpeed : _lastNetUp;
            _lastNetDown = netDown;
            _lastNetUp = netUp;
            _netDown.Text = netDown > 0 ? FormatSpeed(netDown) : "--";
            _netUp.Text = netUp > 0 ? FormatSpeed(netUp) : "--";
            _netDownValues.Add(netDown);
            _netUpValues.Add(netUp);
            if (_netDownValues.Count > 60) _netDownValues.RemoveAt(0);
            if (_netUpValues.Count > 60) _netUpValues.RemoveAt(0);

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
                    var category = _mapping.GetFanCategory(f.Name);
                    switch (category)
                    {
                        case "CPU": cpuFans.Add(f); break;
                        case "GPU": gpuFans.Add(f); break;
                        default: mbFans.Add(f); break;
                    }
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
                var fanCell = new Border { Background = Design.C.InsetB, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6, 10, 6), BorderThickness = new Thickness(1), BorderBrush = CardBorder };
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
                var fanRow = new Border { Background = Design.C.InsetB, CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 6, 10, 6), BorderThickness = new Thickness(1), BorderBrush = CardBorder };
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
        // LiveCharts2 auto-updates via ObservableCollection - no manual drawing needed
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
        if (pct >= 85) return _brush85;
        if (pct >= 60) return _brush60;
        return baseColor;
    }

    private static SolidColorBrush TempColor(double t)
    {
        if (t >= 85) return _brush85;
        if (t >= 70) return _brush60;
        if (t >= 50) return SteamColors.GreenBrush;
        return SteamColors.BlueBrush;
    }

    private static SolidColorBrush FpsValueColor(double fps)
    {
        if (fps >= 60) return _brushGreen;
        if (fps >= 30) return _brushYellow;
        return _brushRed;
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

    // ===== Navigation =====
    
    private void SwitchTab(string tab)
    {
        _hardwareContent.Visibility = tab == "hardware" ? Visibility.Visible : Visibility.Collapsed;
        _stressTestContent.Visibility = tab == "stresstest" ? Visibility.Visible : Visibility.Collapsed;
        _monitoresContent.Visibility = tab == "monitores" ? Visibility.Visible : Visibility.Collapsed;
        _perifericosContent.Visibility = tab == "perifericos" ? Visibility.Visible : Visibility.Collapsed;

        UpdateNavButton(_navHardware, tab == "hardware", "\uE9F5", "Hardware", CpuColor);
        UpdateNavButton(_navStressTest, tab == "stresstest", "\uE7F4", "Stress Test", new SolidColorBrush(ColorFromHex("#EF4444")));
        UpdateNavButton(_navMonitores, tab == "monitores", "\uE7F4", "Monitores", new SolidColorBrush(ColorFromHex("#8B5CF6")));
        UpdateNavButton(_navPerifericos, tab == "perifericos", "\uE711", "Periféricos", new SolidColorBrush(ColorFromHex("#F59E0B")));

        if (tab == "monitores")
            RefreshMonitores();
        if (tab == "perifericos")
        {
            RefreshPerifericos();
            _gamepadTimer.Start();
        }
        else
        {
            _gamepadTimer.Stop();
        }
    }

    private void UpdateNavButton(Button btn, bool isActive, string icon, string label, SolidColorBrush accentColor)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        panel.Children.Add(new FontIcon
        {
            Glyph = icon,
            FontSize = 16,
            Foreground = isActive ? accentColor : Design.C.MutedB,
            FontFamily = new FontFamily("Assets/tabler-icons.ttf#tabler-icons")
        });
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 13,
            FontWeight = isActive ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center
        });
        btn.Content = panel;
        btn.Background = isActive ? new SolidColorBrush(ColorFromHex("#1A2744")) : new SolidColorBrush(Colors.Transparent);
        btn.Foreground = isActive ? new SolidColorBrush(Colors.White) : new SolidColorBrush(ColorFromHex("#94A3B8"));
    }

    // ===== Monitores =====

    private void RefreshMonitores()
    {
        try
        {
            var s = _monitor.Collect();
            var monitoresStack = (StackPanel)_monitoresContent.Content;
            monitoresStack.Children.Clear();

            // Header
            var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
            header.Children.Add(new TextBlock { Text = "Monitores", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) });
            header.Children.Add(new TextBlock { Text = $"{s.Monitors.Count} monitor(es) conectado(s)", FontSize = 13, Foreground = SubtleText });
            monitoresStack.Children.Add(header);

            if (s.Monitors.Count == 0)
            {
                monitoresStack.Children.Add(new TextBlock { Text = "Nenhum monitor detectado", FontSize = 14, Foreground = SubtleText, Margin = new Thickness(0, 16, 0, 0) });
                return;
            }

            // Color for monitors
            var monitorColor = new SolidColorBrush(ColorFromHex("#8B5CF6"));

            foreach (var m in s.Monitors)
            {
                var card = CreateCard();
                var cardStack = new StackPanel { Spacing = 12 };

                // Header with icon
                var hdr = CreateCardHeaderGrid("\uE7F4", m.Name ?? "Monitor", monitorColor, "mon_" + (m.Name ?? "x"));
                cardStack.Children.Add(hdr);

                // Manufacturer and model
                if (!string.IsNullOrEmpty(m.Manufacturer) || !string.IsNullOrEmpty(m.ProductCode))
                {
                    var modelText = $"{m.Manufacturer} {m.ProductCode}".Trim();
                    cardStack.Children.Add(new TextBlock { Text = modelText, FontSize = 11, Foreground = SubtleText, TextTrimming = TextTrimming.CharacterEllipsis });
                }

                // Metrics grid
                var metricsGrid = new Grid { ColumnSpacing = 24 };
                metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Resolution
                if (m.ResolutionWidth > 0)
                {
                    var resStack = new StackPanel { Spacing = 2 };
                    resStack.Children.Add(new TextBlock { Text = "RESOLUÇÃO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
                    resStack.Children.Add(new TextBlock { Text = $"{m.ResolutionWidth}x{m.ResolutionHeight}", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) });
                    metricsGrid.Children.Add(resStack);
                }

                // Refresh rate
                if (m.RefreshRate > 0)
                {
                    var refreshStack = new StackPanel { Spacing = 2 };
                    refreshStack.Children.Add(new TextBlock { Text = "REFRESH", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
                    refreshStack.Children.Add(new TextBlock { Text = $"{m.RefreshRate} Hz", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = monitorColor });
                    Grid.SetColumn(refreshStack, 1);
                    metricsGrid.Children.Add(refreshStack);
                }

                // Size
                if (m.SizeInches > 0)
                {
                    var sizeStack = new StackPanel { Spacing = 2 };
                    sizeStack.Children.Add(new TextBlock { Text = "TAMANHO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
                    sizeStack.Children.Add(new TextBlock { Text = $"{m.SizeInches}\" ({m.WidthCm}x{m.HeightCm}cm)", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) });
                    Grid.SetColumn(sizeStack, 2);
                    metricsGrid.Children.Add(sizeStack);
                }

                cardStack.Children.Add(metricsGrid);

                // Details grid (connection, serial, year)
                var detailsGrid = new Grid { ColumnSpacing = 16, Margin = new Thickness(0, 4, 0, 0) };
                detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                if (!string.IsNullOrEmpty(m.ConnectionType))
                {
                    var connStack = new StackPanel { Spacing = 2 };
                    connStack.Children.Add(new TextBlock { Text = "CONEXÃO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
                    connStack.Children.Add(new TextBlock { Text = m.ConnectionType, FontSize = 13, Foreground = new SolidColorBrush(Colors.White) });
                    detailsGrid.Children.Add(connStack);
                }

                if (!string.IsNullOrEmpty(m.SerialNumber))
                {
                    var serialStack = new StackPanel { Spacing = 2 };
                    serialStack.Children.Add(new TextBlock { Text = "SERIAL", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
                    serialStack.Children.Add(new TextBlock { Text = m.SerialNumber, FontSize = 13, Foreground = new SolidColorBrush(Colors.White) });
                    Grid.SetColumn(serialStack, 1);
                    detailsGrid.Children.Add(serialStack);
                }

                if (detailsGrid.Children.Count > 0)
                    cardStack.Children.Add(detailsGrid);

                // Year info
                if (m.YearOfManufacture > 0)
                {
                    cardStack.Children.Add(new TextBlock { Text = $"Fabricado em {m.YearOfManufacture}", FontSize = 11, Foreground = SubtleText, Margin = new Thickness(0, 4, 0, 0) });
                }

                // Bits per pixel
                if (m.BitsPerPixel > 0)
                {
                    cardStack.Children.Add(new TextBlock { Text = $"Color Depth: {m.BitsPerPixel} bpp", FontSize = 11, Foreground = SubtleText });
                }

                card.Child = cardStack;
                monitoresStack.Children.Add(card);
            }
        }
        catch { }
    }

    // ===== Periféricos =====

    private void RefreshPerifericos()
    {
        try
        {
            var s = _monitor.Collect();
            var stack = (StackPanel)_perifericosContent.Content;
            stack.Children.Clear();

            // Header
            var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
            header.Children.Add(new TextBlock { Text = "Periféricos", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) });
            header.Children.Add(new TextBlock { Text = "Controles e dispositivos conectados", FontSize = 13, Foreground = SubtleText });
            stack.Children.Add(header);

            var gamepadColor = new SolidColorBrush(ColorFromHex("#F59E0B"));

            // Gamepads
            var connectedCount = s.Gamepads.Count(g => g.IsConnected);
            var headerSub = new TextBlock { Text = $"{connectedCount} controle(s) conectado(s)", FontSize = 13, Foreground = SubtleText, Margin = new Thickness(0, -4, 0, 8) };
            stack.Children.Add(headerSub);

            if (s.Gamepads.Count == 0 || connectedCount == 0)
            {
                stack.Children.Add(new TextBlock { Text = "Nenhum controle detectado", FontSize = 14, Foreground = SubtleText, Margin = new Thickness(0, 16, 0, 0) });
                return;
            }

            foreach (var gp in s.Gamepads.Where(g => g.IsConnected))
            {
                var card = CreateCard();
                var cardStack = new StackPanel { Spacing = 12 };

                // Header
                var hdr = CreateCardHeaderGrid("\uE711", gp.Name, gamepadColor, "gp_" + gp.Name);
                cardStack.Children.Add(hdr);

                // Battery info
                if (gp.HasBattery)
                {
                    var batGrid = new Grid { ColumnSpacing = 24 };
                    batGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    batGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var batTypeStack = new StackPanel { Spacing = 2 };
                    batTypeStack.Children.Add(new TextBlock { Text = "TIPO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
                    var batTypeText = gp.BatteryType switch
                    {
                        GamepadBatteryType.Alkaline => "Alcalina",
                        GamepadBatteryType.NiMH => "NiMH",
                        GamepadBatteryType.Wired => "Sem fio (2.4GHz)",
                        _ => "Desconhecida"
                    };
                    batTypeStack.Children.Add(new TextBlock { Text = batTypeText, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) });
                    batGrid.Children.Add(batTypeStack);

                    var batLevelStack = new StackPanel { Spacing = 2 };
                    batLevelStack.Children.Add(new TextBlock { Text = "BATERIA", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
                    var (batText, batColor) = gp.BatteryLevel switch
                    {
                        GamepadBatteryLevel.Full => ("Cheia", new SolidColorBrush(ColorFromHex("#4ADE80"))),
                        GamepadBatteryLevel.Medium => ("Média", gamepadColor),
                        GamepadBatteryLevel.Low => ("Baixa", SteamColors.RedBrush),
                        GamepadBatteryLevel.Empty => ("Vazia", SteamColors.RedBrush),
                        _ => ("--", SubtleText)
                    };
                    batLevelStack.Children.Add(new TextBlock { Text = batText, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = batColor });
                    Grid.SetColumn(batLevelStack, 1);
                    batGrid.Children.Add(batLevelStack);

                    cardStack.Children.Add(batGrid);
                }

                // Polling Rate
                var pollCard = new Border { Background = Design.C.InsetB, CornerRadius = new CornerRadius(8), Padding = new Thickness(16), BorderThickness = new Thickness(1), BorderBrush = CardBorder };
                var pollStack = new StackPanel { Spacing = 8 };
                pollStack.Children.Add(new TextBlock { Text = "POLLING RATE", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
                _gpPollingText = new TextBlock { Text = $"{gp.PollingRate} Hz", FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) };
                pollStack.Children.Add(_gpPollingText);

                // Polling rate bar
                var pollBarStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var pollBarPct = Math.Min(gp.PollingRate / 250.0 * 100, 100);
                var pollBarBg = new Border { Background = Design.C.SecB, CornerRadius = new CornerRadius(4), Height = 6, Width = 200 };
                var pollBarFill = new Border { Background = gamepadColor, CornerRadius = new CornerRadius(4), Height = 6, HorizontalAlignment = HorizontalAlignment.Left };
                pollBarBg.Width = 200;
                pollBarFill.Width = (int)(pollBarPct * 2);
                var pollBarGrid = new Grid();
                pollBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                pollBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200, GridUnitType.Pixel) });
                Grid.SetColumn(pollBarFill, 0);
                Grid.SetColumn(pollBarBg, 0);
                pollBarGrid.Children.Add(pollBarBg);
                pollBarGrid.Children.Add(pollBarFill);
                pollBarStack.Children.Add(pollBarGrid);
                pollBarStack.Children.Add(new TextBlock { Text = $"{gp.PollingRate:F0}", FontSize = 11, Foreground = SubtleText, VerticalAlignment = VerticalAlignment.Center });
                pollStack.Children.Add(pollBarStack);

                pollCard.Child = pollStack;
                cardStack.Children.Add(pollCard);

                // Visual Gamepad
                var gpVisual = CreateGamepadVisual();
                cardStack.Children.Add(gpVisual);

                // Axes info
                var axesCard = new Border { Background = Design.C.InsetB, CornerRadius = new CornerRadius(8), Padding = new Thickness(16), BorderThickness = new Thickness(1), BorderBrush = CardBorder };
                var axesGrid = new Grid { ColumnSpacing = 24, RowSpacing = 12 };
                axesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                axesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                axesGrid.RowDefinitions.Add(new RowDefinition());
                axesGrid.RowDefinitions.Add(new RowDefinition());

                _gpLxText = new TextBlock { Text = "LX", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 };
                _gpLyText = new TextBlock { Text = "LY", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 };
                _gpRxText = new TextBlock { Text = "RX", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 };
                _gpRyText = new TextBlock { Text = "RY", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 };
                _gpLtText = new TextBlock { Text = "LT", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 };
                _gpRtText = new TextBlock { Text = "RT", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 };

                var lxStack = new StackPanel { Spacing = 2 };
                lxStack.Children.Add(_gpLxText);
                var lxVal = new TextBlock { Text = gp.ThumbLX.ToString(), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White), Name = "gpLxVal" };
                lxStack.Children.Add(lxVal);

                var lyStack = new StackPanel { Spacing = 2 };
                lyStack.Children.Add(_gpLyText);
                var lyVal = new TextBlock { Text = gp.ThumbLY.ToString(), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White), Name = "gpLyVal" };
                lyStack.Children.Add(lyVal);

                var rxStack = new StackPanel { Spacing = 2 };
                rxStack.Children.Add(_gpRxText);
                var rxVal = new TextBlock { Text = gp.ThumbRX.ToString(), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White), Name = "gpRxVal" };
                rxStack.Children.Add(rxVal);

                var ryStack = new StackPanel { Spacing = 2 };
                ryStack.Children.Add(_gpRyText);
                var ryVal = new TextBlock { Text = gp.ThumbRY.ToString(), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White), Name = "gpRyVal" };
                ryStack.Children.Add(ryVal);

                var ltStack = new StackPanel { Spacing = 2 };
                ltStack.Children.Add(_gpLtText);
                var ltVal = new TextBlock { Text = gp.LeftTrigger.ToString(), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White), Name = "gpLtVal" };
                ltStack.Children.Add(ltVal);

                var rtStack = new StackPanel { Spacing = 2 };
                rtStack.Children.Add(_gpRtText);
                var rtVal = new TextBlock { Text = gp.RightTrigger.ToString(), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White), Name = "gpRtVal" };
                rtStack.Children.Add(rtVal);

                Grid.SetRow(lxStack, 0); Grid.SetColumn(lxStack, 0);
                Grid.SetRow(lyStack, 0); Grid.SetColumn(lyStack, 1);
                Grid.SetRow(rxStack, 1); Grid.SetColumn(rxStack, 0);
                Grid.SetRow(ryStack, 1); Grid.SetColumn(ryStack, 1);
                axesGrid.Children.Add(lxStack);
                axesGrid.Children.Add(lyStack);
                axesGrid.Children.Add(rxStack);
                axesGrid.Children.Add(ryStack);

                axesCard.Child = axesGrid;
                cardStack.Children.Add(axesCard);

                card.Child = cardStack;
                stack.Children.Add(card);

                _lastGamepadState = gp;
            }
        }
        catch { }
    }

    private Grid CreateGamepadVisual()
    {
        var layout = GamepadLayoutService.Load();
        var grid = new Grid { Width = layout.ImageWidth, Height = layout.ImageHeight, HorizontalAlignment = HorizontalAlignment.Center };

        var img = new Image
        {
            Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/Gamepad/8bitdoUltimate2.png")),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        grid.Children.Add(img);

        var canvas = new Canvas { Width = layout.ImageWidth, Height = layout.ImageHeight, IsHitTestVisible = false };

        // D-Pad individual arms
        if (layout.Buttons.TryGetValue("DPadUp", out var dpUp))
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(dpUp, layout.ImageWidth, layout.ImageHeight);
            _gpBtnUp = CreateOverlayBtn(pr * 1.8, pr * 1.8);
            Canvas.SetLeft(_gpBtnUp, px - pr * 0.9); Canvas.SetTop(_gpBtnUp, py - pr * 0.9);
        }
        if (layout.Buttons.TryGetValue("DPadDown", out var dpDown))
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(dpDown, layout.ImageWidth, layout.ImageHeight);
            _gpBtnDown = CreateOverlayBtn(pr * 1.8, pr * 1.8);
            Canvas.SetLeft(_gpBtnDown, px - pr * 0.9); Canvas.SetTop(_gpBtnDown, py - pr * 0.9);
        }
        if (layout.Buttons.TryGetValue("DPadLeft", out var dpLeft))
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(dpLeft, layout.ImageWidth, layout.ImageHeight);
            _gpBtnLeft = CreateOverlayBtn(pr * 1.8, pr * 1.8);
            Canvas.SetLeft(_gpBtnLeft, px - pr * 0.9); Canvas.SetTop(_gpBtnLeft, py - pr * 0.9);
        }
        if (layout.Buttons.TryGetValue("DPadRight", out var dpRight))
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(dpRight, layout.ImageWidth, layout.ImageHeight);
            _gpBtnRight = CreateOverlayBtn(pr * 1.8, pr * 1.8);
            Canvas.SetLeft(_gpBtnRight, px - pr * 0.9); Canvas.SetTop(_gpBtnRight, py - pr * 0.9);
        }

        // Face buttons
        if (layout.Buttons.TryGetValue("A", out var btnA))
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(btnA, layout.ImageWidth, layout.ImageHeight);
            _gpBtnA = CreateOverlayBtn(pr * 2, pr * 2);
            Canvas.SetLeft(_gpBtnA, px - pr); Canvas.SetTop(_gpBtnA, py - pr);
        }
        if (layout.Buttons.TryGetValue("B", out var btnB))
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(btnB, layout.ImageWidth, layout.ImageHeight);
            _gpBtnB = CreateOverlayBtn(pr * 2, pr * 2);
            Canvas.SetLeft(_gpBtnB, px - pr); Canvas.SetTop(_gpBtnB, py - pr);
        }
        if (layout.Buttons.TryGetValue("X", out var btnX))
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(btnX, layout.ImageWidth, layout.ImageHeight);
            _gpBtnX = CreateOverlayBtn(pr * 2, pr * 2);
            Canvas.SetLeft(_gpBtnX, px - pr); Canvas.SetTop(_gpBtnX, py - pr);
        }
        if (layout.Buttons.TryGetValue("Y", out var btnY))
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(btnY, layout.ImageWidth, layout.ImageHeight);
            _gpBtnY = CreateOverlayBtn(pr * 2, pr * 2);
            Canvas.SetLeft(_gpBtnY, px - pr); Canvas.SetTop(_gpBtnY, py - pr);
        }

        // Analog sticks
        if (layout.Buttons.TryGetValue("LeftStick", out var ls))
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(ls, layout.ImageWidth, layout.ImageHeight);
            _gpBtnLS = CreateOverlayCircle(pr * 2);
            Canvas.SetLeft(_gpBtnLS, px - pr); Canvas.SetTop(_gpBtnLS, py - pr);
        }
        if (layout.Buttons.TryGetValue("RightStick", out var rs))
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(rs, layout.ImageWidth, layout.ImageHeight);
            _gpBtnRS = CreateOverlayCircle(pr * 2);
            Canvas.SetLeft(_gpBtnRS, px - pr); Canvas.SetTop(_gpBtnRS, py - pr);
        }

        // Center buttons
        if (layout.Buttons.TryGetValue("Start", out var start))
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(start, layout.ImageWidth, layout.ImageHeight);
            _gpBtnStart = CreateOverlaySmall(pr * 2, pr * 2);
            Canvas.SetLeft(_gpBtnStart, px - pr); Canvas.SetTop(_gpBtnStart, py - pr);
        }
        if (layout.Buttons.TryGetValue("Back", out var back))
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(back, layout.ImageWidth, layout.ImageHeight);
            _gpBtnBack = CreateOverlaySmall(pr * 2, pr * 2);
            Canvas.SetLeft(_gpBtnBack, px - pr); Canvas.SetTop(_gpBtnBack, py - pr);
        }

        // Shoulder buttons
        if (layout.Buttons.TryGetValue("LeftShoulder", out var lb))
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(lb, layout.ImageWidth, layout.ImageHeight);
            _gpBtnLB = CreateOverlaySmall(pr * 3, pr);
            Canvas.SetLeft(_gpBtnLB, px - pr * 1.5); Canvas.SetTop(_gpBtnLB, py - pr * 0.5);
        }
        if (layout.Buttons.TryGetValue("RightShoulder", out var rb))
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(rb, layout.ImageWidth, layout.ImageHeight);
            _gpBtnRB = CreateOverlaySmall(pr * 3, pr);
            Canvas.SetLeft(_gpBtnRB, px - pr * 1.5); Canvas.SetTop(_gpBtnRB, py - pr * 0.5);
        }

        // Add all overlays to canvas
        canvas.Children.Add(_gpBtnUp);
        canvas.Children.Add(_gpBtnDown);
        canvas.Children.Add(_gpBtnLeft);
        canvas.Children.Add(_gpBtnRight);
        canvas.Children.Add(_gpBtnY);
        canvas.Children.Add(_gpBtnA);
        canvas.Children.Add(_gpBtnX);
        canvas.Children.Add(_gpBtnB);
        canvas.Children.Add(_gpBtnLS);
        canvas.Children.Add(_gpBtnRS);
        canvas.Children.Add(_gpBtnStart);
        canvas.Children.Add(_gpBtnBack);
        canvas.Children.Add(_gpBtnLB);
        canvas.Children.Add(_gpBtnRB);

        grid.Children.Add(canvas);
        return grid;
    }

    private static Border CreateOverlayBtn(double w, double h)
    {
        return new Border
        {
            Width = w, Height = h,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new(2),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0))
        };
    }

    private static Border CreateOverlayCircle(double size)
    {
        return new Border
        {
            Width = size, Height = size,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            CornerRadius = new CornerRadius(size / 2),
            BorderThickness = new(2),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0))
        };
    }

    private static Border CreateOverlaySmall(double w, double h)
    {
        return new Border
        {
            Width = w, Height = h,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new(2),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0))
        };
    }

    // ===== Stress Test =====

    private async void CpuStressBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_stressTest.IsCpuRunning)
        {
            _cpuStressBtn.Content = "Parando...";
            _cpuStressBtn.IsEnabled = false;
            await Task.Run(() => _stressTest.StopCpu());
            _cpuStressBtn.Content = "Iniciar CPU Stress";
            _cpuStressBtn.Background = CpuColor;
            _cpuStressBtn.IsEnabled = true;
            _cpuStressStatus.Text = "Parado";
            _cpuStressStatus.Foreground = SubtleText;
            _cpuStressIndicator.Background = SubtleText;
        }
        else
        {
            _stressTest.StartCpu();
            _cpuStressBtn.Content = "Parar CPU Stress";
            _cpuStressBtn.Background = SteamColors.RedBrush;
            _cpuStressStatus.Text = "Executando...";
            _cpuStressStatus.Foreground = CpuColor;
            _cpuStressIndicator.Background = CpuColor;
        }
    }

    private void GpuStressBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_stressTest.IsGpuRunning)
        {
            _stressTest.StopGpu();
            _gpuStressBtn.Content = "Iniciar GPU Stress";
            _gpuStressBtn.Background = GpuColor;
            _gpuStressStatus.Text = "Parado";
            _gpuStressStatus.Foreground = SubtleText;
            _gpuStressIndicator.Background = SubtleText;
        }
        else
        {
            if (_stressTest.FurMarkPath == null)
            {
                _furmarkStatus.Text = "FurMark não encontrado! Instale o FurMark 2.";
                _furmarkStatus.Foreground = SteamColors.RedBrush;
                return;
            }

            _stressTest.StartGpu();
            _gpuStressBtn.Content = "Parar GPU Stress";
            _gpuStressBtn.Background = SteamColors.RedBrush;
            _gpuStressStatus.Text = "FurMark executando...";
            _gpuStressStatus.Foreground = GpuColor;
            _gpuStressIndicator.Background = GpuColor;
            _furmarkStatus.Text = $"FurMark: {_stressTest.FurMarkPath}";
            _furmarkStatus.Foreground = SubtleText;
        }
    }

    private void UpdateStressTestMetrics()
    {
        // Reuse cached data from Refresh() — no new Collect() call
        var hw = _lastCollect;
        if (hw == null) return;

        // Update model names (once)
        if (_cpuStressModel.Text == "CPU" && !string.IsNullOrEmpty(hw.CpuModel))
            _cpuStressModel.Text = hw.CpuModel;
        if (_gpuStressModel.Text == "GPU" && !string.IsNullOrEmpty(hw.GpuModel))
            _gpuStressModel.Text = hw.GpuModel;

        // CPU Stress Test
        if (_stressTest.IsCpuRunning)
        {
            var elapsed = _stressTest.Elapsed;
            _cpuStressTime.Text = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            _cpuStressLoad.Text = $"{_stressTest.CpuLoad:F0}%";
            _cpuStressLoad.Foreground = UsageColor(_stressTest.CpuLoad, CpuColor);

            _cpuStressTempValues.Add(hw.CpuPackageTemp);
            _cpuStressUsageValues.Add(_stressTest.CpuLoad);
        }
        else
        {
            // Use real LHM data when stress test is not running
            _cpuStressLoad.Text = $"{hw.CpuUsage:F0}%";
            _cpuStressLoad.Foreground = UsageColor(hw.CpuUsage, CpuColor);

            _cpuStressTempValues.Add(hw.CpuPackageTemp);
            _cpuStressUsageValues.Add(hw.CpuUsage);
        }

        // Always update value labels above graphs
        _cpuStressTempValue.Text = hw.CpuPackageTemp > 0 ? $"{hw.CpuPackageTemp:F0}°C" : "--";
        _cpuStressTempValue.Foreground = TempColor(hw.CpuPackageTemp);
        _cpuStressLoadValue.Text = $"{hw.CpuUsage:F0}%";
        _cpuStressLoadValue.Foreground = UsageColor(hw.CpuUsage, CpuColor);

        if (_cpuStressTempValues.Count > 60) _cpuStressTempValues.RemoveAt(0);
        if (_cpuStressUsageValues.Count > 60) _cpuStressUsageValues.RemoveAt(0);

        // Check if FurMark process exited unexpectedly
        if (_stressTest.IsGpuRunning && _stressTest.FurMarkProcessExited)
        {
            _gpuStressBtn.Content = "Iniciar GPU Stress";
            _gpuStressBtn.Background = GpuColor;
            _gpuStressStatus.Text = "FurMark encerrou";
            _gpuStressStatus.Foreground = SubtleText;
            _gpuStressIndicator.Background = SubtleText;
        }

        // GPU monitor (always update when stress test tab is active)
        if (_stressTestContent.Visibility == Visibility.Visible)
        {
            _gpuStressTemp.Text = hw.GpuTemp > 0 ? $"{hw.GpuTemp:F0}°C" : "--";
            _gpuStressTemp.Foreground = TempColor(hw.GpuTemp);
            _gpuStressUsage.Text = $"{hw.GpuUsage:F0}%";
            _gpuStressUsage.Foreground = UsageColor(hw.GpuUsage, GpuColor);
            _gpuStressIndicator.Background = hw.GpuTemp > 80 ? SteamColors.RedBrush : (hw.GpuTemp > 60 ? new SolidColorBrush(ColorFromHex("#F59E0B")) : GpuColor);

            // Update value labels above graphs
            _gpuStressTempValue.Text = hw.GpuTemp > 0 ? $"{hw.GpuTemp:F0}°C" : "--";
            _gpuStressTempValue.Foreground = TempColor(hw.GpuTemp);
            _gpuStressLoadValue.Text = $"{hw.GpuUsage:F0}%";
            _gpuStressLoadValue.Foreground = UsageColor(hw.GpuUsage, GpuColor);

            _gpuStressTempValues.Add(hw.GpuTemp);
            _gpuStressUsageValues.Add(hw.GpuUsage);
            if (_gpuStressTempValues.Count > 60) _gpuStressTempValues.RemoveAt(0);
            if (_gpuStressUsageValues.Count > 60) _gpuStressUsageValues.RemoveAt(0);
        }

        // Graphs auto-update via ObservableCollection - no manual DrawGraph needed
    }

    // ===== Gamepad Real-time =====

    private uint _gpLastPacket = 0;

    private void UpdateGamepadState()
    {
        try
        {
            if (_monitor?.GamepadService == null) return;
            var gp = _monitor.GamepadService.GetState(0);
            if (gp == null || !gp.IsConnected) return;

            // Skip entire UI update if nothing changed
            if (gp.PacketNumber == _gpLastPacket) return;
            _gpLastPacket = gp.PacketNumber;

            // Update polling rate (only text, cheap)
            if (_gpPollingText != null)
                _gpPollingText.Text = $"{gp.PollingRate:F0} Hz";

            // Button state change detection — only repaint buttons that changed
            UpdateButtonOptimized(_gpBtnA, 0, gp.IsPressed(GamepadButton.A));
            UpdateButtonOptimized(_gpBtnB, 1, gp.IsPressed(GamepadButton.B));
            UpdateButtonOptimized(_gpBtnX, 2, gp.IsPressed(GamepadButton.X));
            UpdateButtonOptimized(_gpBtnY, 3, gp.IsPressed(GamepadButton.Y));
            UpdateButtonOptimized(_gpBtnLB, 4, gp.IsPressed(GamepadButton.LeftShoulder));
            UpdateButtonOptimized(_gpBtnRB, 5, gp.IsPressed(GamepadButton.RightShoulder));
            UpdateButtonOptimized(_gpBtnStart, 6, gp.IsPressed(GamepadButton.Start));
            UpdateButtonOptimized(_gpBtnBack, 7, gp.IsPressed(GamepadButton.Back));
            UpdateButtonOptimized(_gpBtnUp, 8, gp.IsPressed(GamepadButton.DPadUp));
            UpdateButtonOptimized(_gpBtnDown, 9, gp.IsPressed(GamepadButton.DPadDown));
            UpdateButtonOptimized(_gpBtnLeft, 10, gp.IsPressed(GamepadButton.DPadLeft));
            UpdateButtonOptimized(_gpBtnRight, 11, gp.IsPressed(GamepadButton.DPadRight));
            UpdateButtonOptimized(_gpBtnLS, 12, gp.IsPressed(GamepadButton.LeftThumb));
            UpdateButtonOptimized(_gpBtnRS, 13, gp.IsPressed(GamepadButton.RightThumb));

            // Axes text (always update — values change continuously)
            if (_gpLxText != null) _gpLxText.Text = $"LX: {gp.ThumbLX}";
            if (_gpLyText != null) _gpLyText.Text = $"LY: {gp.ThumbLY}";
            if (_gpRxText != null) _gpRxText.Text = $"RX: {gp.ThumbRX}";
            if (_gpRyText != null) _gpRyText.Text = $"RY: {gp.ThumbRY}";
            if (_gpLtText != null) _gpLtText.Text = $"LT: {gp.LeftTrigger}";
            if (_gpRtText != null) _gpRtText.Text = $"RT: {gp.RightTrigger}";

            _lastGamepadState = gp;
        }
        catch { }
    }

    private void UpdateButtonOptimized(Border btn, int index, bool isPressed)
    {
        if (btn == null) return;
        if (_gpButtonStates[index] == isPressed) return; // no change — skip repaint
        _gpButtonStates[index] = isPressed;

        btn.Background = isPressed ? GpPressedBg : GpReleasedBg;
        btn.BorderBrush = isPressed ? GpPressedBorder : GpReleasedBorder;
    }
}
