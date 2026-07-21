using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using System.Runtime.InteropServices;
using System.Numerics;
using System.Collections.ObjectModel;
using EMECore.Core.Models;
using EMECore.Core.Services;
using EMECore.Hardware.Services;
using EMECore.WinUI.Theme;
using EMECore.WinUI.Services;
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
    private System.Threading.Timer? _gpPollTimer;
    private System.Threading.Timer? _calibPollTimer;
    private System.Threading.Timer? _batteryTimer;
    private List<PeripheralBatteryInfo> _peripheralBatteries = new();

    // Motherboard
    private TextBlock _mbModel = null!, _mbTemp = null!, _mbVrmTemp = null!, _mbVoltage = null!;
    private StackPanel _mbFansPanel = null!;
    private TextBlock _mbCompactTemp = null!, _mbCompactVrm = null!, _mbCompactFans = null!, _mbCompactVoltage = null!;
    private Grid _mbDetailPanel = null!;

    // RAM
    private TextBlock _ramPct = null!, _ramInfo = null!, _ramModel = null!, _ramVoltage = null!;
    private Grid _ramBar = null!;
    private TextBlock _ramCompactPct = null!, _ramCompactInfo = null!;
    private Grid _ramDetailPanel = null!;

    // CPU
    private TextBlock _cpuPct = null!, _cpuCoreTemp = null!, _cpuPkgTemp = null!, _cpuModel = null!, _cpuVoltage = null!;
    private Grid _cpuBar = null!;
    private CartesianChart _cpuChart = null!;
    private readonly ObservableCollection<double> _cpuValues = new();
    private StackPanel _cpuFansPanel = null!;
    private TextBlock _cpuCompactPct = null!, _cpuCompactCore = null!, _cpuCompactPkg = null!;
    private Grid _cpuDetailPanel = null!;

    // GPU
    private TextBlock _gpuPct = null!, _gpuCoreTemp = null!, _gpuVram = null!, _gpuModel = null!, _gpuVoltage = null!;
    private Grid _gpuBar = null!;
    private CartesianChart _gpuChart = null!;
    private readonly ObservableCollection<double> _gpuValues = new();
    private StackPanel _gpuFansPanel = null!;
    private TextBlock _gpuCompactPct = null!, _gpuCompactTemp = null!, _gpuCompactVram = null!;
    private Grid _gpuDetailPanel = null!;

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
    private TextBlock _fpsStatLow1 = null!, _fpsStatLow01 = null!, _fpsStatFrameTime = null!;
    private Border _fpsCard = null!;
    private Button _fpsToggleBtn = null!;
    private Button _fpsOverlayBtn = null!;
    private FpsOverlayWindow? _fpsOverlay;
    private StackPanel? _fpsSubPanel;
    private Border? _fpsSubSep;

    private int _fanCount;
    private bool _isMoving;

    // Navigation
    private Button _navHardware = null!;
    private Button _navStressTest = null!;
    private Button _navMonitores = null!;
    private Button _navPerifericos = null!;
    private Border _monitorIndicator = null!;
    private List<Button> _monitorNavItems = null!;
    private Button _monitorCollapseBtn = null!;
    private StackPanel _monitorLogoText = null!;
    private Border _monitorLogoBox = null!;
    private TextBlock _monitorNavLbl = null!;
    private bool _monitorCollapsed;
    private ColumnDefinition _monitorSidebarColumn = null!;
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
    private TextBlock _gpStatusText = null!;
    private TextBlock _gpHwCompactStatus = null!;
    private int _gamepadPollCount = 0;
    private Border _gpBtnA = null!, _gpBtnB = null!, _gpBtnX = null!, _gpBtnY = null!;
    private Border _gpBtnLB = null!, _gpBtnRB = null!;
    private Border _gpBtnStart = null!, _gpBtnBack = null!;
    private Border _gpBtnUp = null!, _gpBtnDown = null!, _gpBtnLeft = null!, _gpBtnRight = null!;
    private Border _gpBtnLS = null!, _gpBtnRS = null!;
    private TextBlock _gpPollingText = null!;
    private TextBlock _gpLxText = null!, _gpLyText = null!, _gpRxText = null!, _gpRyText = null!;
    private TextBlock _gpLtText = null!, _gpRtText = null!;
    private FrameworkElement _gpLeftStickCanvas = null!, _gpRightStickCanvas = null!;
    private Ellipse _gpLeftStickDot = null!, _gpRightStickDot = null!;
    private double _gpLsCenterX, _gpLsCenterY, _gpRsCenterX, _gpRsCenterY;
    private GamepadInfo _lastGamepadState = new();
    private Grid? _gpSharedVisual;
    private FrameworkElement? _gpSharedVisualParent;
    private TextBlock? _gpLtIndicator, _gpRtIndicator;
    private Ellipse? _gpLtMiniDot, _gpRtMiniDot;
    private StackPanel? _gpHwHeaderRow;

    // Cached hardware data — Collect() result reused by StressMetrics
    private HardwareStats? _lastCollect;

    // Card collapse state (key = card name, value = isCollapsed)
    private readonly Dictionary<string, bool> _collapsedState = new();
    private readonly Dictionary<string, StackPanel> _expandedContent = new();
    private readonly Dictionary<string, StackPanel> _collapsedContent = new();
    private readonly Dictionary<string, Border> _cardBorders = new();
    private readonly Dictionary<string, Button> _toggleButtons = new();

    // Modular card layout
    private readonly Dictionary<string, Border> _hardwareCards = new();

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
    private static readonly SolidColorBrush GpPressedBg = new(Windows.UI.Color.FromArgb(180, 74, 222, 128));
    private static readonly SolidColorBrush GpPressedBorder = new(Windows.UI.Color.FromArgb(220, 74, 222, 128));
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
        ThemeManager.ThemeChanged += OnThemeChanged;
        _monitor = new HardwareMonitorService();

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 960, Height = 640 });

        // Min size 640x480
        var presenter = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId).Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
        if (presenter != null)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        ApplyWindowTheme(appWindow);

        // Loading overlay — shown FIRST so window appears immediately
        _loadingOverlay = new Grid { Background = SurfaceBg };
        var loadingStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 12 };
        loadingStack.Children.Add(new TextBlock { Text = "E.M.E Core", FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White), HorizontalAlignment = HorizontalAlignment.Center });
        loadingStack.Children.Add(new TextBlock { Text = "Carregando dados do sistema...", FontSize = 13, Foreground = SubtleText, HorizontalAlignment = HorizontalAlignment.Center });
        _loadingOverlay.Children.Add(loadingStack);
        Content = _loadingOverlay;

        // Defer full UI build + data load to after first frame renders
        _ = BuildAndLoadSafelyAsync();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ThemeVisualTree.Refresh(Content as DependencyObject, ThemeManager.Previous, ThemeManager.Current);
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            ApplyWindowTheme(Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId));
        });
    }

    private static void ApplyWindowTheme(Microsoft.UI.Windowing.AppWindow appWindow)
    {
        var theme = ThemeManager.Current;
        var titleBar = appWindow.TitleBar;
        titleBar.ExtendsContentIntoTitleBar = false;
        titleBar.BackgroundColor = theme.Background;
        titleBar.ForegroundColor = theme.TextPrimary;
        titleBar.ButtonBackgroundColor = theme.Background;
        titleBar.ButtonForegroundColor = theme.TextPrimary;
        titleBar.ButtonHoverBackgroundColor = theme.CardHover;
        titleBar.ButtonPressedBackgroundColor = theme.Card;
        titleBar.ButtonInactiveBackgroundColor = theme.Background;
        titleBar.ButtonInactiveForegroundColor = theme.TextMuted;
    }

    private async Task BuildAndLoadSafelyAsync()
    {
        try
        {
            await BuildAndLoadAsync();
        }
        catch (Exception ex)
        {
            WriteMonitorError(ex);
            ShowLoadingError();
        }
    }

    private void ShowLoadingError()
    {
        if (_loadingOverlay == null) return;

        _loadingOverlay.Children.Clear();
        var errorStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 10,
            MaxWidth = 520,
            Margin = new Thickness(24)
        };
        errorStack.Children.Add(new TextBlock
        {
            Text = "Não foi possível carregar o Hardware Monitor.",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });
        errorStack.Children.Add(new TextBlock
        {
            Text = "O erro foi registrado em %LocalAppData%\\EMECore\\Logs\\hardware-monitor.log.",
            FontSize = 12,
            Foreground = SubtleText,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });
        _loadingOverlay.Children.Add(errorStack);
    }

    private static void WriteMonitorError(Exception ex)
    {
        try
        {
            var logDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EMECore",
                "Logs");
            Directory.CreateDirectory(logDirectory);
            var logPath = System.IO.Path.Combine(logDirectory, "hardware-monitor.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
        }
        catch
        {
            // O registro de diagnóstico nunca deve impedir a abertura da interface.
        }
    }

    private bool _isResizing = false;
    private double _lastGoodHeight = 640;

    private async Task BuildAndLoadAsync()
    {
        await Task.Delay(50);
        var root = new Grid { Background = SurfaceBg };
        _monitorSidebarColumn = new ColumnDefinition { Width = new GridLength(232) };
        root.ColumnDefinitions.Add(_monitorSidebarColumn);
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ===== SIDEBAR =====
        var sidebar = new Border
        {
            Background = Design.C.SideB,
            BorderThickness = new Thickness(0, 0, 1, 0), BorderBrush = Design.C.BorB,
        };
        var sidebarGrid = new Grid();
        sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Logo row
        var logoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Design.S.MD };
        var logoBox = new Border { Width=40, Height=40, CornerRadius=Design.R.XL, Background=Design.C.Pri10B, BorderThickness=new Thickness(1), BorderBrush=new SolidColorBrush(Design.C.PriRing), VerticalAlignment=VerticalAlignment.Center, Child=new TextBlock{Text="EME",FontSize=11,FontWeight=Microsoft.UI.Text.FontWeights.Bold,CharacterSpacing=-50,Foreground=Design.C.PriB,FontFamily=new("Consolas"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center} };
        logoRow.Children.Add(logoBox);
        _monitorLogoText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        _monitorLogoText.Children.Add(new TextBlock { Text = "E.M.E Core", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Design.C.FgB });
        _monitorLogoText.Children.Add(new TextBlock { Text = "Hardware Monitor", FontSize = 10, Foreground = Design.C.Muted70B, FontFamily = new("Consolas"), CharacterSpacing = 100 });
        logoRow.Children.Add(_monitorLogoText);
        _monitorLogoBox = logoBox;
        var logoBorder = new Border { Padding = new Thickness(Design.S.XL, Design.S.XL, Design.S.XL, Design.S.LG), Child = logoRow };
        Grid.SetRow(logoBorder, 0); sidebarGrid.Children.Add(logoBorder);

        // Collapse button
        _monitorCollapseBtn = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Design.S.SM, Children = { new FontIcon { Glyph = "\uE76B", FontSize = 14, FontFamily = new FontFamily("Segoe MDL2 Assets"), Foreground = Design.C.MutedB }, new TextBlock { Text = "Recolher", FontSize = 11, Foreground = Design.C.MutedB, VerticalAlignment = VerticalAlignment.Center, CharacterSpacing = 30 } } },
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(Design.S.MD, 6, Design.S.MD, 6), Margin = new Thickness(Design.S.MD, 0, Design.S.MD, Design.S.LG),
            CornerRadius = Design.R.MD
        };
        _monitorCollapseBtn.Click += (_, _) =>
        {
            _monitorCollapsed = !_monitorCollapsed;
            ApplyMonitorCollapsedState(_monitorCollapsed);
            SettingsService.Set("monitor_sidebar_collapsed", _monitorCollapsed.ToString());
        };
        Grid.SetRow(_monitorCollapseBtn, 1); sidebarGrid.Children.Add(_monitorCollapseBtn);

        // Section label
        _monitorNavLbl = new TextBlock { Text = "Monitoramento", FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Design.C.Muted70B, Padding = new Thickness(Design.S.XL, 0, Design.S.XL, 0), Margin = new Thickness(0, 0, 0, Design.S.SM), CharacterSpacing = 180 };
        Grid.SetRow(_monitorNavLbl, 2); sidebarGrid.Children.Add(_monitorNavLbl);

        // Nav items container with indicator
        _monitorIndicator = new Border { Width=3, HorizontalAlignment=HorizontalAlignment.Left, CornerRadius=new CornerRadius(0,3,3,0), Background=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(0,1),GradientStops={new(){Color=Design.C.Pri,Offset=0},new(){Color=Design.C.Pri,Offset=1}}}, VerticalAlignment=VerticalAlignment.Top };
        var navStack = new StackPanel { Padding = new Thickness(Design.S.MD, 0, Design.S.MD, 0) };

        _navHardware = CreateNavItem("\uE9CA", "Hardware", CpuColor);
        _navStressTest = CreateNavItem("\uE7F4", "Stress Test", new SolidColorBrush(ColorFromHex("#EF4444")));
        _navMonitores = CreateNavItem("\uE7F4", "Monitores", new SolidColorBrush(ColorFromHex("#8B5CF6")));
        _navPerifericos = CreateNavItem("\uE961", "Periféricos", new SolidColorBrush(ColorFromHex("#F59E0B")));

        _navHardware.Click += (_, _) => SwitchTab("hardware");
        _navMonitores.Click += (_, _) => SwitchTab("monitores");
        _navPerifericos.Click += (_, _) => SwitchTab("perifericos");

        navStack.Children.Add(_navHardware);
        navStack.Children.Add(_navMonitores);
        navStack.Children.Add(_navPerifericos);

        var navContainer = new Grid();
        navContainer.Children.Add(_monitorIndicator);
        navContainer.Children.Add(navStack);
        Grid.SetRow(navContainer, 3); sidebarGrid.Children.Add(navContainer);

        _monitorNavItems = new List<Button> { _navHardware, _navMonitores, _navPerifericos };
        ActivateMonitorNav(_navHardware);

        sidebar.Child = sidebarGrid;
        Grid.SetColumn(sidebar, 0);
        root.Children.Add(sidebar);

        // Restore collapsed state
        if (SettingsService.Get("monitor_sidebar_collapsed", "False") == "True")
        {
            _monitorCollapsed = true;
            ApplyMonitorCollapsedState(true);
        }

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

        // ===== Motherboard Card =====
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

        // MB Details
        var (mbDetailToggle, mbDetail) = CreateDetailSection("DETALHES", MbColor);
        _mbDetailPanel = mbDetail;
        mbStack.Children.Add(mbDetailToggle);
        mbStack.Children.Add(mbDetail);

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
        _hardwareCards["mb"] = mbCard;

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

        // RAM Details
        var (ramDetailToggle, ramDetail) = CreateDetailSection("DETALHES", RamColor);
        _ramDetailPanel = ramDetail;
        ramStack.Children.Add(ramDetailToggle);
        ramStack.Children.Add(ramDetail);

        var ramCompact = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, Padding = new Thickness(0, 4, 0, 4) };
        _ramCompactPct = new TextBlock { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = RamColor };
        _ramCompactInfo = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Colors.White) };
        ramCompact.Children.Add(new TextBlock { Text = "USO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        ramCompact.Children.Add(_ramCompactPct);
        ramCompact.Children.Add(new TextBlock { Text = "RAM", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        ramCompact.Children.Add(_ramCompactInfo);
        RegisterCard("ram", ramCard, ramStack, ramCompact);
        _hardwareCards["ram"] = ramCard;

        // ===== RAM Card =====
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

        // CPU Details
        var (cpuDetailToggle, cpuDetail) = CreateDetailSection("DETALHES", CpuColor);
        _cpuDetailPanel = cpuDetail;
        cpuStack.Children.Add(cpuDetailToggle);
        cpuStack.Children.Add(cpuDetail);

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
        _hardwareCards["cpu"] = cpuCard;

        // ===== GPU Card =====
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
        var gpuVramStack = new StackPanel { Spacing = 2 };
        gpuVramStack.Children.Add(new TextBlock { Text = "VRAM", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50 });
        _gpuVram = new TextBlock { FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) };
        gpuVramStack.Children.Add(_gpuVram);
        Grid.SetColumn(gpuVramStack, 2);
        gpuMetricsGrid.Children.Add(gpuVramStack);
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

        // GPU Details
        var (gpuDetailToggle, gpuDetail) = CreateDetailSection("DETALHES", GpuColor);
        _gpuDetailPanel = gpuDetail;
        gpuStack.Children.Add(gpuDetailToggle);
        gpuStack.Children.Add(gpuDetail);

        var gpuCompact = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, Padding = new Thickness(0, 4, 0, 4) };
        _gpuCompactPct = new TextBlock { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = GpuColor };
        _gpuCompactTemp = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Colors.White) };
        _gpuCompactVram = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Colors.White) };
        gpuCompact.Children.Add(new TextBlock { Text = "USO", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        gpuCompact.Children.Add(_gpuCompactPct);
        gpuCompact.Children.Add(new TextBlock { Text = "TEMP", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        gpuCompact.Children.Add(_gpuCompactTemp);
        gpuCompact.Children.Add(new TextBlock { Text = "VRAM", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        gpuCompact.Children.Add(_gpuCompactVram);
        RegisterCard("gpu", gpuCard, gpuStack, gpuCompact);
        _hardwareCards["gpu"] = gpuCard;

        // ===== Disk Card =====
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
        _hardwareCards["disk"] = diskCard;

        // ===== Network Card =====
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
        _hardwareCards["net"] = netCard;
        RestoreCardStates();

        // ===== FPS Card =====
        _fpsCard = CreateCard();
        _fpsCard.VerticalAlignment = VerticalAlignment.Top;
        var fpsStack = new StackPanel { Spacing = 12 };

        var fpsHeaderGrid = new Grid();
        fpsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fpsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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

        _fpsOverlayBtn = new Button
        {
            Content = "\uE945",
            FontSize = 12,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
            Foreground = SubtleText,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(1),
            BorderBrush = SubtleText,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTipService.SetToolTip(_fpsOverlayBtn, "Overlay de FPS na tela do jogo");
        _fpsOverlayBtn.Click += FpsOverlay_Click;
        Grid.SetColumn(_fpsOverlayBtn, 2);
        fpsHeaderGrid.Children.Add(_fpsOverlayBtn);
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

        var low1Item = new StackPanel { Spacing = 2 };
        low1Item.Children.Add(new TextBlock { Text = "1% LOW", FontSize = 9, Foreground = new SolidColorBrush(ColorFromHex("#475569")), CharacterSpacing = 50 });
        _fpsStatLow1 = new TextBlock { Text = "--", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SubtleText };
        low1Item.Children.Add(_fpsStatLow1);
        fpsStatsGrid.Children.Add(low1Item);

        var low01Item = new StackPanel { Spacing = 2 };
        low01Item.Children.Add(new TextBlock { Text = "0.1% LOW", FontSize = 9, Foreground = new SolidColorBrush(ColorFromHex("#475569")), CharacterSpacing = 50 });
        _fpsStatLow01 = new TextBlock { Text = "--", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SubtleText };
        low01Item.Children.Add(_fpsStatLow01);
        Grid.SetColumn(low01Item, 1); fpsStatsGrid.Children.Add(low01Item);

        var ftItem = new StackPanel { Spacing = 2 };
        ftItem.Children.Add(new TextBlock { Text = "FRAME TIME", FontSize = 9, Foreground = new SolidColorBrush(ColorFromHex("#475569")), CharacterSpacing = 50 });
        _fpsStatFrameTime = new TextBlock { Text = "--", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = SubtleText };
        ftItem.Children.Add(_fpsStatFrameTime);
        Grid.SetColumn(ftItem, 2); fpsStatsGrid.Children.Add(ftItem);
        fpsDetailsStack.Children.Add(fpsStatsGrid);
        Grid.SetColumn(fpsDetailsStack, 1);
        fpsGrid.Children.Add(fpsDetailsStack);
        fpsStack.Children.Add(fpsGrid);

        var fpsSubSep = new Border { Height = 1, Background = new SolidColorBrush(ColorFromHex("#1E293B")), Margin = new Thickness(0, 4, 0, 4), Visibility = Visibility.Collapsed };
        _fpsSubPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Visibility = Visibility.Collapsed };
        _fpsSubPanel.Children.Add(CreateFpsToggleChip("1% Low", "overlay_show_low1"));
        _fpsSubPanel.Children.Add(CreateFpsToggleChip("0.1% Low", "overlay_show_low01"));
        _fpsSubPanel.Children.Add(CreateFpsToggleChip("Frame Time", "overlay_show_frametime"));
        fpsStack.Children.Add(fpsSubSep);
        fpsStack.Children.Add(_fpsSubPanel);
        _fpsSubSep = fpsSubSep;

        _fpsCard.Child = fpsStack;
        _hardwareCards["fps"] = _fpsCard;

        // ===== Gamepad Card (inline in Hardware tab) =====
        var gpHwCard = CreateCard();
        var gpHwStack = new StackPanel { Spacing = 12 };
        var gpHwHeader = CreateCardHeaderGrid("\uE7FC", "CONTROLE", new SolidColorBrush(ColorFromHex("#4ADE80")), "gamepad", "Segoe MDL2 Assets");
        _gpHwHeaderRow = gpHwHeader;

        var miniTrigColor = new SolidColorBrush(ColorFromHex("#4ADE80"));
        var miniSize = 24.0;

        var ltMiniCanvas = new Canvas { Width = miniSize, Height = miniSize, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
        var ltMiniDot = new Ellipse { Width = 6, Height = 6, Fill = miniTrigColor };
        Canvas.SetLeft(ltMiniDot, 4); Canvas.SetTop(ltMiniDot, 4);
        ltMiniCanvas.Children.Add(ltMiniDot);
        var ltMiniCurve = new Microsoft.UI.Xaml.Shapes.Path
        {
            Stroke = miniTrigColor, StrokeThickness = 1.5,
            Data = new PathGeometry { Figures = new PathFigureCollection { new PathFigure
            {
                StartPoint = new Windows.Foundation.Point(2, 2),
                Segments = new PathSegmentCollection { new BezierSegment
                {
                    Point1 = new Windows.Foundation.Point(2 + 7, 2),
                    Point2 = new Windows.Foundation.Point(22, 22 - 8),
                    Point3 = new Windows.Foundation.Point(22, 22)
                }}
            }}}
        };
        ltMiniCanvas.Children.Add(ltMiniCurve);
        _gpLtMiniDot = ltMiniDot;
        gpHwHeader.Children.Add(ltMiniCanvas);

        var rtMiniCanvas = new Canvas { Width = miniSize, Height = miniSize, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
        var rtMiniDot = new Ellipse { Width = 6, Height = 6, Fill = miniTrigColor };
        Canvas.SetLeft(rtMiniDot, 14); Canvas.SetTop(rtMiniDot, 4);
        rtMiniCanvas.Children.Add(rtMiniDot);
        var rtMiniCurve = new Microsoft.UI.Xaml.Shapes.Path
        {
            Stroke = miniTrigColor, StrokeThickness = 1.5,
            Data = new PathGeometry { Figures = new PathFigureCollection { new PathFigure
            {
                StartPoint = new Windows.Foundation.Point(22, 2),
                Segments = new PathSegmentCollection { new BezierSegment
                {
                    Point1 = new Windows.Foundation.Point(22 - 7, 2),
                    Point2 = new Windows.Foundation.Point(2, 22 - 8),
                    Point3 = new Windows.Foundation.Point(2, 22)
                }}
            }}}
        };
        rtMiniCanvas.Children.Add(rtMiniCurve);
        _gpRtMiniDot = rtMiniDot;
        gpHwHeader.Children.Add(rtMiniCanvas);
        gpHwStack.Children.Add(gpHwHeader);
        _gpStatusText = new TextBlock { Text = "Verificando...", FontSize = 11, Foreground = SubtleText };
        gpHwStack.Children.Add(_gpStatusText);

        _gpSharedVisual = CreateGamepadVisual();
        gpHwStack.Children.Add(_gpSharedVisual);

        var gpHwCompact = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, Padding = new Thickness(0, 4, 0, 4) };
        _gpHwCompactStatus = new TextBlock { Text = "Aguardando...", FontSize = 12, Foreground = SubtleText };
        gpHwCompact.Children.Add(new TextBlock { Text = "CONTROLE", FontSize = 9, Foreground = SubtleText, CharacterSpacing = 50, VerticalAlignment = VerticalAlignment.Center });
        gpHwCompact.Children.Add(_gpHwCompactStatus);
        RegisterCard("gamepad", gpHwCard, gpHwStack, gpHwCompact);
        _hardwareCards["gamepad"] = gpHwCard;

        // Build dynamic card layout
        BuildHardwareLayout(contentStack);
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

        SizeChanged += (_, args) =>
        {
            if (_isResizing) return;
            var w = args.Size.Width;
            var h = args.Size.Height;
            if (w >= 640 && h >= 100)
            {
                _lastGoodHeight = h;
            }
            if (w < 640)
            {
                _isResizing = true;
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                var wid = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var aw = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(wid);
                aw.Resize(new Windows.Graphics.SizeInt32 { Width = 640, Height = (int)_lastGoodHeight });
                _isResizing = false;
                return;
            }
            if (_hwGrid != null)
            {
                var cols = _hwGrid.ColumnDefinitions.Count;
                if (w < 700 && cols == 2)
                {
                    _hwGrid.ColumnDefinitions.RemoveAt(1);
                    RelayoutHwGrid();
                }
                else if (w >= 700 && cols == 1)
                {
                    _hwGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    RelayoutHwGrid();
                }
            }
            UpdateDetailGridColumns(w);
            UpdateGamepadCompactMode(w);
        };

        Closed += (_, _) => { ThemeManager.ThemeChanged -= OnThemeChanged; _fpsOverlay?.Dispose(); _bgTimer?.Dispose(); _gpPollTimer?.Dispose(); _batteryTimer?.Dispose(); _graphTimer.Stop(); _stressTimer.Stop(); _gamepadTimer.Stop(); _stressTest.Dispose(); _monitor.Dispose(); };
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
                _lastCollect = s;
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
        _gamepadTimer.Tick += (_, _) => UpdateGamepadUi();
        _gamepadTimer.Start();

        _gpPollTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                _monitor?.GamepadService?.PollState(0);
            }
            catch { }
        }, null, 0, 1);

        // Peripheral battery refresh (every 30s)
        var batteryService = new PeripheralBatteryService();
        _ = RefreshPeripheralBatteriesAsync(batteryService);
        _batteryTimer = new System.Threading.Timer(async _ =>
        {
            await RefreshPeripheralBatteriesAsync(batteryService);
        }, null, 2000, 30000);

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
        try { RefreshLhm(); } catch { }

        //_timer.Start();
        _graphTimer.Start();
        // _stressTimer.Start(); — oculto com a aba Stress Test

        if (_loadingOverlay != null)
            _loadingOverlay.Visibility = Visibility.Collapsed;

        // PHASE 2: WMI slow data (RAM/Disk/Network/Monitors) — background
        _ = LoadWmiInBackgroundAsync();
    }

    private async Task RefreshPeripheralBatteriesAsync(PeripheralBatteryService batteryService)
    {
        try
        {
            var batteries = await batteryService.GetBatteriesAsync();
            _peripheralBatteries = batteries;
            DispatcherQueue.TryEnqueue(RefreshPerifericosBatteries);
        }
        catch
        {
            // Um provedor de bateria não pode interromper o restante do monitor.
        }
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

    private StackPanel CreateCardHeaderGrid(string icon, string title, SolidColorBrush accentColor,
        string cardKey, string iconFontFamily = "Assets/tabler-icons.ttf#tabler-icons")
    {
        var hdr = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        hdr.Children.Add(new FontIcon { Glyph = icon, FontSize = 18, Foreground = accentColor, FontFamily = new FontFamily(iconFontFamily) });
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
            var game = _monitor.DetectRunningGame();
            _monitor.StartFpsMonitor(game ?? "");
            _fpsRunning = true;
            _fpsToggleBtn.Content = "PARAR";
            _fpsToggleBtn.Foreground = _brushRed;
            _fpsToggleBtn.BorderBrush = _brushRed;
        }
    }

    private void FpsOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (_fpsOverlay != null && _fpsOverlay.IsActive)
        {
            _fpsOverlay.Stop();
            _fpsOverlayBtn.Foreground = SubtleText;
            _fpsOverlayBtn.BorderBrush = SubtleText;
            if (_fpsSubPanel != null) _fpsSubPanel.Visibility = Visibility.Collapsed;
            if (_fpsSubSep != null) _fpsSubSep.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (_fpsOverlay == null)
                _fpsOverlay = new FpsOverlayWindow(_monitor.FpsMonitor);
            _fpsOverlay.Start();
            _fpsOverlayBtn.Foreground = FpsColor;
            _fpsOverlayBtn.BorderBrush = FpsColor;
            if (_fpsSubPanel != null) _fpsSubPanel.Visibility = Visibility.Visible;
            if (_fpsSubSep != null) _fpsSubSep.Visibility = Visibility.Visible;
            ApplyFpsToggles();
        }
    }

    private Button CreateFpsToggleChip(string label, string settingKey)
    {
        bool active = SettingsService.Get(settingKey, "1") == "1";
        var icon = new TextBlock
        {
            Text = active ? "\uE73A" : "\uE738",
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
            FontSize = 11,
            Foreground = active ? FpsColor : SubtleText,
            VerticalAlignment = VerticalAlignment.Center
        };
        var txt = new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = active ? FpsColor : SubtleText,
            VerticalAlignment = VerticalAlignment.Center
        };
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        stack.Children.Add(icon);
        stack.Children.Add(txt);
        var tag = new FpsToggleTag { Key = settingKey, Icon = icon, Txt = txt };
        var btn = new Button
        {
            Content = stack,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(1),
            BorderBrush = active ? FpsColor : SubtleText,
            Padding = new Thickness(6, 3, 6, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = tag
        };
        btn.Click += FpsToggleChip_Click;
        return btn;
    }

    private void FpsToggleChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not FpsToggleTag tag) return;
        string key = tag.Key;
        TextBlock icon = tag.Icon;
        TextBlock txt = tag.Txt;
        bool active = SettingsService.Get(key, "1") == "1";
        bool newState = !active;
        SettingsService.Set(key, newState ? "1" : "0");
        icon.Text = newState ? "\uE73A" : "\uE738";
        icon.Foreground = newState ? FpsColor : SubtleText;
        txt.Foreground = newState ? FpsColor : SubtleText;
        btn.BorderBrush = newState ? FpsColor : SubtleText;
        ApplyFpsToggles();
    }

    private void ApplyFpsToggles()
    {
        _fpsOverlay?.UpdateDisplayOptions(
            SettingsService.Get("overlay_show_low1", "1") == "1",
            SettingsService.Get("overlay_show_low01", "1") == "1",
            SettingsService.Get("overlay_show_frametime", "1") == "1");
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

        SaveCardStates();
    }

    private void SaveCardStates()
    {
        var entries = _collapsedState.Select(kv => $"\"{kv.Key}\":{kv.Value.ToString().ToLower()}");
        SettingsService.Set("monitor_card_states", "{" + string.Join(",", entries) + "}");
    }

    private void RestoreCardStates()
    {
        var json = SettingsService.Get("monitor_card_states", "{}");
        if (json.Length < 3) return;
        var inner = json.Trim('{', '}');
        foreach (var pair in inner.Split(','))
        {
            var parts = pair.Split(':');
            if (parts.Length != 2) continue;
            var key = parts[0].Trim('"');
            var val = parts[1].Trim() == "true";
            if (!_collapsedState.ContainsKey(key)) continue;
            if (val == _collapsedState[key]) continue;

            _collapsedState[key] = val;
            if (_toggleButtons.TryGetValue(key, out var toggleBtn))
                toggleBtn.Content = val ? "▲" : "▼";
            if (_expandedContent.TryGetValue(key, out var expanded))
                expanded.Visibility = val ? Visibility.Collapsed : Visibility.Visible;
            if (_collapsedContent.TryGetValue(key, out var collapsed))
                collapsed.Visibility = val ? Visibility.Visible : Visibility.Collapsed;
        }
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

        if (header != null && header is FrameworkElement fe)
        {
            Grid.SetColumn(fe, 0);
            headerRow.Children.Add(fe);
        }

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };

        var toggleBtn = new Button
        {
            Content = toggleGlyph,
            FontSize = 12,
            Foreground = SubtleText,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 4, 6, 4),
            Tag = key
        };
        toggleBtn.Click += CardToggle_Click;
        _toggleButtons[key] = toggleBtn;

        btnRow.Children.Add(toggleBtn);

        if (CardLayoutService.IsOptional(key))
        {
            var removeBtn = new Button
            {
                Content = "\uE711",
                FontSize = 10,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Foreground = SubtleText,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 2, 4, 2),
                Tag = key
            };
            removeBtn.Click += RemoveCard_Click;
            btnRow.Children.Add(removeBtn);
        }

        Grid.SetColumn(btnRow, 1);
        headerRow.Children.Add(btnRow);

        var container = new StackPanel { Spacing = 12, VerticalAlignment = VerticalAlignment.Top };
        container.Children.Add(headerRow);
        container.Children.Add(expanded);
        container.Children.Add(collapsed);
        card.Child = container;
        card.VerticalAlignment = VerticalAlignment.Top;
    }

    private void ToggleCardVisibility(string key, bool show)
    {
        if (!_hardwareCards.TryGetValue(key, out var card)) return;
        card.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show && key == "gamepad")
        {
            if (card.Child is StackPanel sp && sp.Children.Count > 2)
                MoveGamepadVisualTo(sp);
        }
    }

    private void AddCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item || item.Tag is not string key) return;
        CardLayoutService.ShowCard(key);
        ToggleCardVisibility(key, true);
        RefreshAddButton();
    }

    private void RemoveCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string key) return;
        CardLayoutService.HideCard(key);
        ToggleCardVisibility(key, false);
        RefreshAddButton();
    }

    private Button? _addCardBtn;

    private void RefreshAddButton()
    {
        if (_addCardBtn == null) return;
        var addable = CardLayoutService.GetAddableCards();
        _addCardBtn.Visibility = addable.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        var flyout = new MenuFlyout();
        foreach (var key in addable)
        {
            var item = new MenuFlyoutItem { Text = CardLayoutService.GetCardDisplayName(key), Tag = key };
            item.Click += AddCard_Click;
            flyout.Items.Add(item);
        }
        _addCardBtn.Flyout = flyout;
    }

    private Grid? _hwGrid;

    private void BuildHardwareLayout(StackPanel contentStack)
    {
        var allKeys = CardLayoutService.GetOrder();
        var cards = new List<Border>();
        foreach (var key in allKeys)
        {
            if (_hardwareCards.TryGetValue(key, out var card))
            {
                card.Visibility = CardLayoutService.IsCardVisible(key) ? Visibility.Visible : Visibility.Collapsed;
                cards.Add(card);
            }
        }

        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var col = i % 2;
            var row = i / 2;

            while (grid.RowDefinitions.Count <= row)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(card, row);
            Grid.SetColumn(card, col);
            grid.Children.Add(card);
        }

        var addable = CardLayoutService.GetAddableCards();
        var addRow = (cards.Count + 1) / 2;
        while (grid.RowDefinitions.Count <= addRow)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var addBtn = new Button
        {
            Content = "+ Adicionar módulo",
            FontSize = 13,
            Foreground = SubtleText,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderBrush = SubtleText,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(4),
            Visibility = addable.Count > 0 ? Visibility.Visible : Visibility.Collapsed
        };
        _addCardBtn = addBtn;

        var flyout = new MenuFlyout();
        foreach (var key in addable)
        {
            var item = new MenuFlyoutItem { Text = CardLayoutService.GetCardDisplayName(key), Tag = key };
            item.Click += AddCard_Click;
            flyout.Items.Add(item);
        }
        addBtn.Flyout = flyout;

        Grid.SetRow(addBtn, addRow);
        Grid.SetColumnSpan(addBtn, 2);
        grid.Children.Add(addBtn);

        _hwGrid = grid;
        contentStack.Children.Add(grid);
    }

    private void RelayoutHwGrid()
    {
        if (_hwGrid == null) return;
        var cols = _hwGrid.ColumnDefinitions.Count;
        var children = _hwGrid.Children.Cast<FrameworkElement>().ToList();
        _hwGrid.Children.Clear();
        _hwGrid.RowDefinitions.Clear();

        int row = 0, col = 0;
        foreach (var child in children)
        {
            if (col >= cols) { row++; col = 0; }
            while (_hwGrid.RowDefinitions.Count <= row)
                _hwGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(child, row);
            Grid.SetColumn(child, col);
            _hwGrid.Children.Add(child);
            col++;
        }
    }

    private void UpdateGamepadCompactMode(double width)
    {
        if (_gpSharedVisual == null) return;
        var isCompact = width < 700;
        _gpSharedVisual.MaxWidth = isCompact ? Math.Max(width - 80, 200) : double.PositiveInfinity;
        _gpSharedVisual.HorizontalAlignment = isCompact ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
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

    private static (Button toggle, Grid detailGrid) CreateDetailSection(string label, SolidColorBrush accentColor)
    {
        var toggleBtn = new Button
        {
            Content = $"  {label}  ",
            FontSize = 10,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 4, 0, 0),
            CornerRadius = new CornerRadius(4),
            Background = Design.C.SecB,
            Foreground = SubtleText,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var detailGrid = new Grid
        {
            Margin = new Thickness(0, 6, 0, 0),
            Visibility = Visibility.Collapsed,
            ColumnSpacing = 12,
            RowSpacing = 2
        };
        detailGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        detailGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        toggleBtn.Click += (_, _) =>
        {
            detailGrid.Visibility = detailGrid.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
            toggleBtn.Content = detailGrid.Visibility == Visibility.Visible
                ? $"  {label} ▲" : $"  {label} ▼";
        };

        return (toggleBtn, detailGrid);
    }

    private static TextBlock MakeDetailLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = new SolidColorBrush(ColorFromHex("#9CA3AF")),
            Margin = new Thickness(0, 2, 0, 0)
        };
    }

    private static void AddSectionLabel(Grid panel, string text)
    {
        var rowIdx = panel.RowDefinitions.Count;
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var label = MakeDetailLabel(text);
        Grid.SetRow(label, rowIdx);
        Grid.SetColumnSpan(label, panel.ColumnDefinitions.Count);
        panel.Children.Add(label);
    }

    private static void AddDetailRow(Grid panel, string label, string value)
    {
        var colCount = panel.ColumnDefinitions.Count;
        var rowCount = panel.RowDefinitions.Count;

        var cell = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cell.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cell.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = new SolidColorBrush(ColorFromHex("#6B7280")), TextTrimming = TextTrimming.CharacterEllipsis });
        var val = new TextBlock { Text = value, FontSize = 11, Foreground = new SolidColorBrush(Colors.White), HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(val, 1);
        cell.Children.Add(val);

        int row, col;
        if (rowCount == 0)
        {
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            row = 0; col = 0;
        }
        else
        {
            row = rowCount - 1;
            var lastRowCells = 0;
            var hasFullSpan = false;
            foreach (var child in panel.Children)
            {
                if (child is FrameworkElement fe && Grid.GetRow(fe) == row)
                {
                    if (Grid.GetColumnSpan(fe) >= colCount)
                        hasFullSpan = true;
                    else
                        lastRowCells++;
                }
            }
            if (hasFullSpan || lastRowCells >= colCount)
            {
                panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                row = rowCount;
                col = 0;
            }
            else
            {
                col = lastRowCells;
            }
        }

        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, col);
        panel.Children.Add(cell);
    }

    private Button CreateNavItem(string icon, string label, SolidColorBrush accentColor)
    {
        var btn = new Button
        {
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Foreground = Design.C.FgB,
            BorderThickness = new Thickness(0),
            CornerRadius = Design.R.LG,
            Padding = new Thickness(Design.S.MD, Design.S.SM, Design.S.MD, Design.S.SM),
        };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Design.S.MD };
        panel.Children.Add(new FontIcon { Glyph = icon, FontSize = 18, Foreground = Design.C.MutedB, FontFamily = new FontFamily("Segoe MDL2 Assets") });
        panel.Children.Add(new TextBlock { Text = label, FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        btn.Content = panel;

        btn.PointerEntered += (_, _) => { if (!_monitorNavItems.Contains(btn) || !IsMonitorNavActive(btn)) btn.Background = Design.C.SecB; };
        btn.PointerExited += (_, _) => { if (!IsMonitorNavActive(btn)) btn.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent); };

        return btn;
    }

    private bool IsMonitorNavActive(Button btn)
    {
        var idx = _monitorNavItems.IndexOf(btn);
        return idx >= 0 && _currentMonitorTab == idx;
    }

    private int _currentMonitorTab = 0;

    private void ActivateMonitorNav(Button active)
    {
        for (int i = 0; i < _monitorNavItems.Count; i++)
        {
            var btn = _monitorNavItems[i];
            var isActive = btn == active;
            if (isActive) _currentMonitorTab = i;

            btn.Background = isActive ? Design.C.Pri10B : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            btn.Foreground = isActive ? Design.C.FgB : Design.C.FgB;
            btn.BorderThickness = isActive ? new Thickness(1) : new Thickness(0);
            btn.BorderBrush = isActive ? new SolidColorBrush(Design.C.PriRing20) : null;

            if (btn.Content is StackPanel panel && panel.Children.Count >= 2)
            {
                if (panel.Children[0] is FontIcon icon)
                    icon.Foreground = isActive ? Design.C.PriB : Design.C.MutedB;
                if (panel.Children[1] is TextBlock text)
                    text.FontWeight = isActive ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
            }
        }

        // Reposition indicator
        var idx2 = _monitorNavItems.IndexOf(active);
        if (idx2 >= 0)
        {
            var y = 0.0;
            for (int i = 0; i < idx2; i++)
                y += _monitorNavItems[i].ActualHeight + 8; // margin
            _monitorIndicator.Margin = new Thickness(0, (int)y + 8, 0, 0);
        }
    }

    private void ApplyMonitorCollapsedState(bool collapsed)
    {
        ((FontIcon)((StackPanel)_monitorCollapseBtn.Content).Children[0]).Glyph = collapsed ? "\uE76C" : "\uE76B";
        ((TextBlock)((StackPanel)_monitorCollapseBtn.Content).Children[1]).Text = collapsed ? "" : "Recolher";
        _monitorCollapseBtn.HorizontalContentAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        _monitorLogoBox.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        _monitorLogoText.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        _monitorNavLbl.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        _monitorIndicator.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        _monitorSidebarColumn.Width = new GridLength(collapsed ? 64 : 232);
        foreach (var btn in _monitorNavItems)
        {
            if (btn.Content is StackPanel panel && panel.Children.Count >= 2)
            {
                if (panel.Children[1] is TextBlock text)
                    text.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            }
            btn.HorizontalContentAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            btn.Padding = collapsed ? new Thickness(0, Design.S.SM, 0, Design.S.SM) : new Thickness(Design.S.MD, Design.S.SM, Design.S.MD, Design.S.SM);
        }
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
            _gpuVram.Text = FormatGpuMemory(s.GpuMemoryTotalMb);
            _gpuCompactVram.Text = FormatGpuMemory(s.GpuMemoryTotalMb);

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
                _fpsStatLow1.Text = $"{s.FpsLow1:F1}";
                _fpsStatLow01.Text = $"{s.FpsLow01:F1}";
                _fpsStatFrameTime.Text = $"{s.FpsFrameTimeMs:F1} ms";
            }
            else
            {
                _fpsValue.Text = "--";
                _fpsValue.Foreground = SubtleText;
                _fpsLabel.Text = s.FpsSource != "Off" ? $"Aguardando... {s.FpsSource}" :
                    (_monitor.DetectedGame != null ? $"Jogo detectado: {_monitor.DetectedGame} — clique INICIAR" : "Nenhum jogo detectado");
                _fpsInfo.Text = "";
                _fpsStatLow1.Text = "--";
                _fpsStatLow01.Text = "--";
                _fpsStatFrameTime.Text = "--";
            }

            // CPU/GPU detail panels (LHM fast data)
            UpdateCpuDetails(s);
            UpdateGpuDetails(s);
            UpdateMbDetails(s);

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

            UpdateRamDetails(s);
        }
        catch { }
    }

    private void UpdateCpuDetails(HardwareStats s)
    {
        _cpuDetailPanel.Children.Clear();
        _cpuDetailPanel.RowDefinitions.Clear();
        if (s.CpuCoresPhysical > 0) AddDetailRow(_cpuDetailPanel, "Núcleos / Threads", $"{s.CpuCoresPhysical}C / {s.CpuThreads}T");
        if (s.CpuArchitecture != "") AddDetailRow(_cpuDetailPanel, "Arquitetura", s.CpuArchitecture);
        if (s.CpuSocket != "") AddDetailRow(_cpuDetailPanel, "Socket", s.CpuSocket);
        if (s.CpuBaseClock > 0) AddDetailRow(_cpuDetailPanel, "Clock Base", $"{s.CpuBaseClock:F2} GHz");
        if (s.CpuMaxClock > 0) AddDetailRow(_cpuDetailPanel, "Clock Max", $"{s.CpuMaxClock:F2} GHz");
        if (s.CpuL2Cache > 0) AddDetailRow(_cpuDetailPanel, "Cache L2", $"{s.CpuL2Cache:F0} MB");
        if (s.CpuL3Cache > 0) AddDetailRow(_cpuDetailPanel, "Cache L3", $"{s.CpuL3Cache:F0} MB");
        if (s.CpuCoreLoads.Count > 0)
        {
            AddSectionLabel(_cpuDetailPanel, "Carga por Core");
            foreach (var cl in s.CpuCoreLoads)
                AddDetailRow(_cpuDetailPanel, cl.Name, $"{cl.Value:F0}%");
        }
        if (s.CpuCoreTemps.Count > 0)
        {
            AddSectionLabel(_cpuDetailPanel, "Temperatura por Core");
            foreach (var ct in s.CpuCoreTemps)
                AddDetailRow(_cpuDetailPanel, ct.Name, $"{ct.Value:F0}°C");
        }
        if (s.CpuCoreClocks.Count > 0)
        {
            AddSectionLabel(_cpuDetailPanel, "Clock por Core");
            foreach (var ck in s.CpuCoreClocks)
                AddDetailRow(_cpuDetailPanel, ck.Name, $"{ck.Value:F0} MHz");
        }
    }

    private void UpdateGpuDetails(HardwareStats s)
    {
        _gpuDetailPanel.Children.Clear();
        _gpuDetailPanel.RowDefinitions.Clear();
        if (s.GpuManufacturer != "") AddDetailRow(_gpuDetailPanel, "Fabricante", s.GpuManufacturer);
        if (s.GpuDriverVersion != "") AddDetailRow(_gpuDetailPanel, "Driver", s.GpuDriverVersion);
        if (s.GpuCoreClockMhz > 0) AddDetailRow(_gpuDetailPanel, "Clock Core", $"{s.GpuCoreClockMhz:F0} MHz");
        if (s.GpuMemoryClockMhz > 0) AddDetailRow(_gpuDetailPanel, "Clock Memória", $"{s.GpuMemoryClockMhz:F0} MHz");
        if (s.GpuMemoryTotalMb > 0) AddDetailRow(_gpuDetailPanel, "VRAM Total", $"{s.GpuMemoryTotalMb:F0} MB");
        if (s.GpuMemoryUsedMb > 0) AddDetailRow(_gpuDetailPanel, "VRAM Usada", $"{s.GpuMemoryUsedMb:F0} MB");
        if (s.GpuMemoryType != "") AddDetailRow(_gpuDetailPanel, "Processador", s.GpuMemoryType);
        if (s.GpuHotspotTemp > 0) AddDetailRow(_gpuDetailPanel, "Hotspot", $"{s.GpuHotspotTemp:F0}°C");
        if (s.GpuFanSpeeds.Count > 0)
        {
            foreach (var f in s.GpuFanSpeeds)
                AddDetailRow(_gpuDetailPanel, f.Name, $"{f.Value:F0} RPM");
        }
        if (s.GpuClocks.Count > 0)
        {
            AddSectionLabel(_gpuDetailPanel, "Clocks");
            foreach (var c in s.GpuClocks)
                AddDetailRow(_gpuDetailPanel, c.Name, $"{c.Value:F0} MHz");
        }
    }

    private static string FormatGpuMemory(double memoryMb)
    {
        return memoryMb > 0 ? $"{memoryMb / 1024.0:F0} GB" : "--";
    }

    private void UpdateMbDetails(HardwareStats s)
    {
        _mbDetailPanel.Children.Clear();
        _mbDetailPanel.RowDefinitions.Clear();
        if (s.MbBiosVersion != "") AddDetailRow(_mbDetailPanel, "BIOS", s.MbBiosVersion);
        if (s.MbBiosDate != "") AddDetailRow(_mbDetailPanel, "Data BIOS", s.MbBiosDate);
        if (s.MbSerialNumber != "") AddDetailRow(_mbDetailPanel, "Serial", s.MbSerialNumber);
        if (s.MotherboardChipsetTemp > 0) AddDetailRow(_mbDetailPanel, "Chipset", $"{s.MotherboardChipsetTemp:F0}°C");
        if (s.MotherboardSocketTemp > 0) AddDetailRow(_mbDetailPanel, "Socket", $"{s.MotherboardSocketTemp:F0}°C");
        if (s.MotherboardPcieTemp > 0) AddDetailRow(_mbDetailPanel, "PCIe", $"{s.MotherboardPcieTemp:F0}°C");
        if (s.Voltage12V > 0) AddDetailRow(_mbDetailPanel, "+12V", $"{s.Voltage12V:F2}V");
        if (s.Voltage5V > 0) AddDetailRow(_mbDetailPanel, "+5V", $"{s.Voltage5V:F2}V");
        if (s.Voltage33V > 0) AddDetailRow(_mbDetailPanel, "+3.3V", $"{s.Voltage33V:F2}V");
        if (s.Voltage5Vsb > 0) AddDetailRow(_mbDetailPanel, "5VSB", $"{s.Voltage5Vsb:F2}V");
        if (s.VoltageDram > 0) AddDetailRow(_mbDetailPanel, "DRAM", $"{s.VoltageDram:F2}V");
        if (s.VoltageCpuSoc > 0) AddDetailRow(_mbDetailPanel, "CPU SoC", $"{s.VoltageCpuSoc:F2}V");
        if (s.VoltageCmosBattery > 0) AddDetailRow(_mbDetailPanel, "Bateria", $"{s.VoltageCmosBattery:F2}V");
        if (s.MbTemperatures.Count > 0)
        {
            AddSectionLabel(_mbDetailPanel, "Temperaturas");
            foreach (var t in s.MbTemperatures)
                AddDetailRow(_mbDetailPanel, t.Name, $"{t.Value:F1}°C");
        }
        if (s.MbVoltages.Count > 0)
        {
            AddSectionLabel(_mbDetailPanel, "Tensões");
            foreach (var v in s.MbVoltages)
                AddDetailRow(_mbDetailPanel, v.Name, $"{v.Value:F3}V");
        }
        if (s.MbFanDuties.Count > 0)
        {
            AddSectionLabel(_mbDetailPanel, "Duty Cycles");
            foreach (var f in s.MbFanDuties)
                AddDetailRow(_mbDetailPanel, f.Name, $"{f.DutyPercent:F1}%");
        }
    }

    private void UpdateRamDetails(HardwareStats s)
    {
        _ramDetailPanel.Children.Clear();
        _ramDetailPanel.RowDefinitions.Clear();
        if (s.RamType != "") AddDetailRow(_ramDetailPanel, "Tipo", s.RamType);
        if (s.RamFormFactor != "") AddDetailRow(_ramDetailPanel, "Formato", s.RamFormFactor);
        if (s.RamSpeed > 0) AddDetailRow(_ramDetailPanel, "Velocidade", $"{s.RamSpeed} MHz");
        if (s.RamMaxSpeed > 0) AddDetailRow(_ramDetailPanel, "Velocidade Máx", $"{s.RamMaxSpeed} MHz");
        if (s.RamVoltage > 0) AddDetailRow(_ramDetailPanel, "Tensão", $"{s.RamVoltage:F1}V");
        if (s.RamFree > 0) AddDetailRow(_ramDetailPanel, "Livre", $"{s.RamFree:F1} GB");
        if (s.RamVirtualTotal > 0) AddDetailRow(_ramDetailPanel, "Virtual Total", $"{s.RamVirtualTotal:F1} GB");
        if (s.RamVirtualUsed > 0) AddDetailRow(_ramDetailPanel, "Virtual Usada", $"{s.RamVirtualUsed:F1} GB");
        if (s.RamModules.Count > 0)
        {
            AddSectionLabel(_ramDetailPanel, $"Módulos ({s.RamModules.Count})");
            foreach (var m in s.RamModules)
            {
                AddDetailRow(_ramDetailPanel, m.Slot, $"{m.CapacityGb:F0}GB {m.MemoryType} @ {m.SpeedMHz}MHz");
                if (!string.IsNullOrEmpty(m.Manufacturer))
                    AddDetailRow(_ramDetailPanel, "  Fabricante", m.Manufacturer);
            }
        }
    }

    private void UpdateDetailGridColumns(double windowWidth)
    {
        var grids = new[] { _cpuDetailPanel, _gpuDetailPanel, _mbDetailPanel, _ramDetailPanel };
        var twoCols = windowWidth >= 800;
        foreach (var g in grids)
        {
            if (g == null) continue;
            if (twoCols && g.ColumnDefinitions.Count < 2)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            else if (!twoCols && g.ColumnDefinitions.Count > 1)
            {
                g.ColumnDefinitions.RemoveAt(1);
            }
        }
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
            _gpuVram.Text = FormatGpuMemory(s.GpuMemoryTotalMb);
            _gpuCompactVram.Text = FormatGpuMemory(s.GpuMemoryTotalMb);

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
                _fpsStatLow1.Text = $"{s.FpsLow1:F1}";
                _fpsStatLow01.Text = $"{s.FpsLow01:F1}";
                _fpsStatFrameTime.Text = $"{s.FpsFrameTimeMs:F1} ms";
            }
            else
            {
                _fpsValue.Text = "--";
                _fpsValue.Foreground = SubtleText;
                _fpsLabel.Text = s.FpsSource != "Off" ? $"Aguardando... {s.FpsSource}" :
                    (_monitor.DetectedGame != null ? $"Jogo detectado: {_monitor.DetectedGame} — clique INICIAR" : "Nenhum jogo detectado");
                _fpsInfo.Text = "";
                _fpsStatLow1.Text = "--";
                _fpsStatLow01.Text = "--";
                _fpsStatFrameTime.Text = "--";
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

        var activeBtn = tab switch
        {
            "hardware" => _navHardware,
            "stresstest" => _navStressTest,
            "monitores" => _navMonitores,
            "perifericos" => _navPerifericos,
            _ => _navHardware
        };
        ActivateMonitorNav(activeBtn);

        if (tab == "monitores")
            RefreshMonitores();
        if (tab == "perifericos")
        {
            RefreshPerifericos();
        }

        if (tab == "hardware")
        {
            if (_hardwareCards.TryGetValue("gamepad", out var gpCard) && gpCard.Child is StackPanel sp && sp.Children.Count > 2)
            {
                MoveGamepadVisualTo(sp);
            }
        }

        if (tab == "hardware" || tab == "perifericos")
        {
            _gamepadTimer.Start();
        }
        else
        {
            _gamepadTimer.Stop();
        }
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

    private int _perifBuildCount = 0;
    private void RefreshPerifericosWith(HardwareStats s)
    {
        _perifBuildCount++;
        
        try
        {
            var stack = (StackPanel)_perifericosContent.Content;
            stack.Children.Clear();

            // Header
            var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
            header.Children.Add(new TextBlock { Text = "Periféricos", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) });
            header.Children.Add(new TextBlock { Text = "Controles e dispositivos conectados", FontSize = 13, Foreground = SubtleText });
            stack.Children.Add(header);

            var gamepadColor = new SolidColorBrush(ColorFromHex("#4ADE80"));

            // Gamepads
            var connectedCount = s.Gamepads.Count(g => g.IsConnected);
            var headerSub = new TextBlock { Text = $"{connectedCount} controle(s) conectado(s)", FontSize = 13, Foreground = SubtleText, Margin = new Thickness(0, -4, 0, 8) };
            stack.Children.Add(headerSub);

            if (s.Gamepads.Count == 0 || connectedCount == 0)
            {
                stack.Children.Add(new TextBlock { Text = "Nenhum controle detectado", FontSize = 14, Foreground = SubtleText, Margin = new Thickness(0, 16, 0, 0) });
            }
            else
            {

            foreach (var gp in s.Gamepads.Where(g => g.IsConnected))
            {
                var card = CreateCard();
                var cardStack = new StackPanel { Spacing = 12 };

                // Header
                var hdr = CreateCardHeaderGrid("\uE7FC", gp.Name, gamepadColor, "gp_" + gp.Name, "Segoe MDL2 Assets");
                cardStack.Children.Add(hdr);

                // Calibrate button
                var calibrateBtn = new Button
                {
                    Content = "\uE771  Calibrar Layout",
                    FontSize = 11,
                    FontFamily = new FontFamily("Assets/tabler-icons.ttf#tabler-icons"),
                    Foreground = gamepadColor,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    BorderThickness = new Thickness(1),
                    BorderBrush = gamepadColor,
                    Padding = new Thickness(12, 6, 12, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                calibrateBtn.Click += (_, _) =>
                {
                    ShowCalibrationOverlay();
                };
                cardStack.Children.Add(calibrateBtn);

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

                // Move shared gamepad visual here
                MoveGamepadVisualTo(cardStack);

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
        }
        catch { }
        RefreshPerifericosBatteries();
    }

    private void MoveGamepadVisualTo(Panel newParent)
    {
        if (_gpSharedVisual == null) return;
        var oldParent = _gpSharedVisual.Parent as Panel;
        oldParent?.Children.Remove(_gpSharedVisual);
        newParent.Children.Add(_gpSharedVisual);
        Array.Fill(_gpButtonStates, false);
        _gpLastPacket = 0;
        _gpSharedVisual.UpdateLayout();
    }

    private void RecreateGamepadVisual()
    {
        if (_gpSharedVisual == null) return;
        var oldParent = _gpSharedVisual.Parent as Panel;
        oldParent?.Children.Remove(_gpSharedVisual);
        _gpSharedVisual = CreateGamepadVisual();
        if (oldParent != null)
            oldParent.Children.Add(_gpSharedVisual);
        Array.Fill(_gpButtonStates, false);
        _gpLastPacket = 0;
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
        // Left Stick dot
        if (layout.Buttons.TryGetValue("LeftStick", out var lsPos))
        {
            var (lsx, lsy, _) = GamepadLayoutService.ToPixels(lsPos, layout.ImageWidth, layout.ImageHeight);
            _gpLsCenterX = lsx; _gpLsCenterY = lsy;
            _gpLeftStickDot = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(ColorFromHex("#4ADE80")) };
            Canvas.SetLeft(_gpLeftStickDot, lsx - 5);
            Canvas.SetTop(_gpLeftStickDot, lsy - 5);
            canvas.Children.Add(_gpLeftStickDot);
        }

        // Right Stick dot
        if (layout.Buttons.TryGetValue("RightStick", out var rsPos))
        {
            var (rsx, rsy, _) = GamepadLayoutService.ToPixels(rsPos, layout.ImageWidth, layout.ImageHeight);
            _gpRsCenterX = rsx; _gpRsCenterY = rsy;
            _gpRightStickDot = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(ColorFromHex("#4ADE80")) };
            Canvas.SetLeft(_gpRightStickDot, rsx - 5);
            Canvas.SetTop(_gpRightStickDot, rsy - 5);
            canvas.Children.Add(_gpRightStickDot);
        }

        // Trigger curves (outside the image, left and right sides)
        var trigColor = new SolidColorBrush(ColorFromHex("#4ADE80"));
        var trigStroke = 2.0;
        var lw = layout.ImageWidth;
        var lh = layout.ImageHeight;

        foreach (var btn in new[] { _gpBtnUp, _gpBtnDown, _gpBtnLeft, _gpBtnRight, _gpBtnY, _gpBtnA, _gpBtnX, _gpBtnB, _gpBtnLS, _gpBtnRS, _gpBtnStart, _gpBtnBack, _gpBtnLB, _gpBtnRB })
        {
            if (btn != null) canvas.Children.Add(btn);
        }

        grid.Children.Add(canvas);
        return grid;
    }

    private static FrameworkElement CreateAnalogVisualizer(SolidColorBrush color, out Ellipse dot)
    {
        const int size = 140;
        const int margin = 10;
        var canvas = new Canvas { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center };

        var bg = new Rectangle { Width = size, Height = size, RadiusX = 8, RadiusY = 8, Fill = Design.C.InsetB };
        canvas.Children.Add(bg);

        var crossH = new Line { X1 = margin, Y1 = size / 2.0, X2 = size - margin, Y2 = size / 2.0, Stroke = Design.C.SecB, StrokeThickness = 1 };
        var crossV = new Line { X1 = size / 2.0, Y1 = margin, X2 = size / 2.0, Y2 = size - margin, Stroke = Design.C.SecB, StrokeThickness = 1 };
        canvas.Children.Add(crossH);
        canvas.Children.Add(crossV);

        var outer = new Ellipse { Width = size - 20, Height = size - 20, Stroke = color, StrokeThickness = 1.5 };
        Canvas.SetLeft(outer, 10); Canvas.SetTop(outer, 10);
        canvas.Children.Add(outer);

        dot = new Ellipse { Width = 10, Height = 10, Fill = color };
        Canvas.SetLeft(dot, size / 2.0 - 5);
        Canvas.SetTop(dot, size / 2.0 - 5);
        canvas.Children.Add(dot);

        var label = new TextBlock { Text = "X:0  Y:0", FontSize = 10, Foreground = SubtleText, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };

        var wrapper = new StackPanel { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Center };
        wrapper.Children.Add(canvas);
        wrapper.Children.Add(label);
        return wrapper;
    }

    private static double Bezier(double t, double p0, double p1, double p2, double p3)
    {
        double u = 1 - t;
        return u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
    }

    private static double MiniBezier(double t, double p0, double p1, double p2, double p3) =>
        Bezier(t, p0, p1, p2, p3);

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

    // ===== Gamepad Calibration Inline =====

    private FrameworkElement? _calibrationDragTarget;
    private string? _calibrationDragKey;
    private double _calibrationOffsetX, _calibrationOffsetY;
    private readonly Dictionary<FrameworkElement, string> _calibrationOverlays = new();
    private GamepadLayout _calibrationLayout = new();

    private void ShowCalibrationOverlay()
    {
        _gamepadTimer.Stop(); // pausa timer durante calibração

        var stack = (StackPanel)_perifericosContent.Content;
        stack.Children.Clear();

        _calibrationLayout = GamepadLayoutService.Load();
        _calibrationOverlays.Clear();

        var header = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        header.Children.Add(new TextBlock { Text = "Calibração do Controle", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White) });
        header.Children.Add(new TextBlock { Text = "Arraste os círculos sobre os botões da imagem", FontSize = 13, Foreground = SubtleText });
        stack.Children.Add(header);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 0, 0, 16) };
        var saveBtn = new Button { Content = "Salvar Layout", Foreground = new SolidColorBrush(ColorFromHex("#F59E0B")), Padding = new Thickness(24, 8, 24, 8) };
        saveBtn.Click += (_, _) =>
        {
            GamepadLayoutService.Save(_calibrationLayout);
            GamepadLayoutService.InvalidateCache();
            RecreateGamepadVisual();
            saveBtn.Content = "Salvo!";
            saveBtn.Foreground = new SolidColorBrush(ColorFromHex("#4ADE80"));
        };
        btnRow.Children.Add(saveBtn);

        var resetBtn = new Button { Content = "Resetar", Padding = new Thickness(24, 8, 24, 8) };
        resetBtn.Click += (_, _) => { GamepadLayoutService.InvalidateCache(); ShowCalibrationOverlay(); };
        btnRow.Children.Add(resetBtn);

        var backBtn = new Button { Content = "Voltar", Padding = new Thickness(24, 8, 24, 8) };
        backBtn.Click += (_, _) =>
        {
            _calibPollTimer?.Dispose();
            _calibPollTimer = null;
            if (_gpSharedVisual?.Parent is Panel p) p.Children.Remove(_gpSharedVisual);
            RecreateGamepadVisual();
            RefreshPerifericos();
            _gamepadTimer.Start();
        };
        btnRow.Children.Add(backBtn);
        stack.Children.Add(btnRow);

        var canvas = new Canvas
        {
            Width = _calibrationLayout.ImageWidth,
            Height = _calibrationLayout.ImageHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = Design.C.InsetB
        };

        var imgPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Gamepad", "8bitdoUltimate2.png");
        var img = new Image
        {
            Source = new BitmapImage(new Uri(imgPath)),
            Width = _calibrationLayout.ImageWidth,
            Height = _calibrationLayout.ImageHeight
        };
        canvas.Children.Add(img);

        var accent = new SolidColorBrush(ColorFromHex("#4ADE80"));
        foreach (var (key, btn) in _calibrationLayout.Buttons)
        {
            var (px, py, pr) = GamepadLayoutService.ToPixels(btn, (int)canvas.Width, (int)canvas.Height);
            var d = Math.Max(12, pr * 2);

            var circle = new Border
            {
                Width = d, Height = d,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(80, 245, 158, 11)),
                BorderBrush = accent,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(d / 2),
                Tag = key
            };
            var label = new TextBlock
            {
                Text = key, FontSize = 8, Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            circle.Child = label;
            Canvas.SetLeft(circle, px - pr);
            Canvas.SetTop(circle, py - pr);
            canvas.Children.Add(circle);

            circle.PointerPressed += (sender, e) =>
            {
                if (sender is not Border b) return;
                _calibrationDragTarget = b;
                _calibrationDragKey = b.Tag as string;
                b.CapturePointer(e.Pointer);
                var pos = e.GetCurrentPoint(canvas);
                _calibrationOffsetX = pos.Position.X - Canvas.GetLeft(b);
                _calibrationOffsetY = pos.Position.Y - Canvas.GetTop(b);
                b.Opacity = 1.0;
                e.Handled = true;
            };

            circle.PointerMoved += (sender, e) =>
            {
                if (sender is not Border b || _calibrationDragTarget != b) return;
                var pos = e.GetCurrentPoint(canvas);
                var nx = Math.Clamp(pos.Position.X - _calibrationOffsetX, -b.Width * 0.3, canvas.Width - b.Width * 0.7);
                var ny = Math.Clamp(pos.Position.Y - _calibrationOffsetY, -b.Height * 0.3, canvas.Height - b.Height * 0.7);
                Canvas.SetLeft(b, nx);
                Canvas.SetTop(b, ny);
            };

            circle.PointerReleased += (sender, e) =>
            {
                if (sender is not Border b) return;
                b.Opacity = 0.85;
                var cx = Canvas.GetLeft(b) + b.Width / 2;
                var cy = Canvas.GetTop(b) + b.Height / 2;
                var savedKey = _calibrationDragKey;
                _calibrationDragTarget?.ReleasePointerCapture(e.Pointer);
                _calibrationDragTarget = null;
                if (savedKey != null && _calibrationLayout.Buttons.ContainsKey(savedKey))
                {
                    var nx = Math.Round(cx / canvas.Width, 3);
                    var ny = Math.Round(cy / canvas.Height, 3);
                    _calibrationLayout.Buttons[savedKey].X = nx;
                    _calibrationLayout.Buttons[savedKey].Y = ny;
                }
            };

            circle.PointerCaptureLost += (_, _) => { _calibrationDragTarget = null; _calibrationDragKey = null; };

            _calibrationOverlays[circle] = key;
        }

        // Add live dots for sticks
        _gpLeftStickDot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(ColorFromHex("#4ADE80")), Visibility = Visibility.Collapsed };
        canvas.Children.Add(_gpLeftStickDot);
        _gpRightStickDot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(ColorFromHex("#4ADE80")), Visibility = Visibility.Collapsed };
        canvas.Children.Add(_gpRightStickDot);

        stack.Children.Add(canvas);

        // Live stick dot indicators on calibration canvas
        _gpLeftStickDot.Visibility = Visibility.Visible;
        _gpRightStickDot.Visibility = Visibility.Visible;
        _calibPollTimer?.Dispose();
        _calibPollTimer = new System.Threading.Timer(_ =>
        {
            try
            {
                if (_monitor?.GamepadService == null) return;
                var gp = _monitor.GamepadService.GetCachedState(0);
                if (gp == null || !gp.IsConnected) return;
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        const double r = 30.0;
                        if (_gpLeftStickDot != null && _calibrationLayout.Buttons.TryGetValue("LeftStick", out var ls))
                        {
                            var (lsx, lsy, _) = GamepadLayoutService.ToPixels(ls, (int)canvas.Width, (int)canvas.Height);
                            Canvas.SetLeft(_gpLeftStickDot, lsx + (gp.ThumbLX / 32767.0) * r - 4);
                            Canvas.SetTop(_gpLeftStickDot, lsy - (gp.ThumbLY / 32767.0) * r - 4);
                        }
                        if (_gpRightStickDot != null && _calibrationLayout.Buttons.TryGetValue("RightStick", out var rs))
                        {
                            var (rsx, rsy, _) = GamepadLayoutService.ToPixels(rs, (int)canvas.Width, (int)canvas.Height);
                            Canvas.SetLeft(_gpRightStickDot, rsx + (gp.ThumbRX / 32767.0) * r - 4);
                            Canvas.SetTop(_gpRightStickDot, rsy - (gp.ThumbRY / 32767.0) * r - 4);
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }, null, 0, 16);
    }

    // ===== Gamepad Real-time =====

    private int _lastGamepadCount = -1;

    private void RefreshPerifericosIfChanged()
    {
        try
        {
            var s = _lastCollect;
            if (s == null) return;
            var count = s.Gamepads.Count(g => g.IsConnected);
            if (count != _lastGamepadCount)
            {
                _lastGamepadCount = count;
                RefreshPerifericosWith(s);
            }
        }
        catch { }
    }

    private void RefreshPerifericosBatteries()
    {
        try
        {
            var stack = (StackPanel)_perifericosContent.Content;
            if (stack == null) return;

            // Remove old battery cards and header (find by Tag)
            for (int i = stack.Children.Count - 1; i >= 0; i--)
            {
                if (stack.Children[i] is Border b && b.Tag is string tag && (tag == "battery-section" || tag == "battery-card"))
                {
                    stack.Children.RemoveAt(i);
                }
                else if (stack.Children[i] is Grid grid && grid.Tag is string gridTag && gridTag == "battery-section")
                {
                    stack.Children.RemoveAt(i);
                }
                else if (stack.Children[i] is TextBlock tb && tb.Tag is string tbTag && tbTag == "battery-header")
                {
                    stack.Children.RemoveAt(i);
                }
            }

            if (_peripheralBatteries.Count == 0) return;

            var batHeader = new TextBlock
            {
                Text = $"{_peripheralBatteries.Count} periférico(s) detectado(s)",
                FontSize = 13,
                Foreground = SubtleText,
                Margin = new Thickness(0, 16, 0, 8),
                Tag = "battery-header"
            };
            stack.Children.Add(batHeader);

            var batteryGrid = new Grid
            {
                ColumnSpacing = 12,
                RowSpacing = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = "battery-section"
            };

            foreach (var bat in _peripheralBatteries)
            {
                var card = CreateCard();
                card.Tag = "battery-card";
                card.HorizontalAlignment = HorizontalAlignment.Stretch;
                var cardStack = new StackPanel { Spacing = 8 };

                var hdr = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                hdr.Children.Add(CreatePeripheralTypeIcon(bat.DeviceName));
                hdr.Children.Add(new TextBlock { Text = bat.DeviceName, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
                cardStack.Children.Add(hdr);

                var batColor = bat.BatteryPercent switch
                {
                    < 0 => new SolidColorBrush(ColorFromHex("#9CA3AF")),
                    < 15 => SteamColors.RedBrush,
                    < 30 => new SolidColorBrush(ColorFromHex("#F59E0B")),
                    _ => new SolidColorBrush(ColorFromHex("#4ADE80"))
                };
                var batPctText = bat.BatteryPercent >= 0 ? $"{bat.BatteryPercent}%" : "N/A";

                var batRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                batRow.Children.Add(new TextBlock { Text = batPctText, FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = batColor, VerticalAlignment = VerticalAlignment.Center });
                batRow.Children.Add(new TextBlock { Text = bat.Status, FontSize = 12, Foreground = SubtleText, VerticalAlignment = VerticalAlignment.Center });
                cardStack.Children.Add(batRow);

                if (bat.SupportsPollingRate)
                {
                    var pollingText = bat.PollingRateHz > 0
                        ? $"Polling rate: {bat.PollingRateHz} Hz"
                        : "Polling rate: Detectando...";
                    cardStack.Children.Add(new TextBlock { Text = pollingText, FontSize = 12, Foreground = SubtleText });
                }

                // Battery bar
                if (bat.BatteryPercent >= 0)
                {
                    var barGrid = new Grid { Height = 6, CornerRadius = new CornerRadius(3), Background = Design.C.SecB };
                    barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(bat.BatteryPercent, GridUnitType.Star) });
                    barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - bat.BatteryPercent, GridUnitType.Star) });
                    var barFill = new Border { CornerRadius = new CornerRadius(3), Background = batColor, HorizontalAlignment = HorizontalAlignment.Stretch };
                    Grid.SetColumn(barFill, 0);
                    barGrid.Children.Add(barFill);
                    cardStack.Children.Add(barGrid);
                }

                card.Child = cardStack;
                batteryGrid.Children.Add(card);
            }

            ArrangePeripheralCards(batteryGrid, stack.ActualWidth);
            batteryGrid.SizeChanged += (_, args) => ArrangePeripheralCards(batteryGrid, args.NewSize.Width);
            stack.Children.Add(batteryGrid);
        }
        catch { }
    }

    private static void ArrangePeripheralCards(Grid grid, double availableWidth)
    {
        var columnCount = availableWidth >= 680 ? 2 : 1;
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();

        for (var column = 0; column < columnCount; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var rowCount = (int)Math.Ceiling(grid.Children.Count / (double)columnCount);
        for (var row = 0; row < rowCount; row++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var index = 0; index < grid.Children.Count; index++)
        {
            if (grid.Children[index] is not FrameworkElement card) continue;
            Grid.SetColumn(card, index % columnCount);
            Grid.SetRow(card, index / columnCount);
        }
    }

    private void RefreshPerifericos()
    {
        var s = _monitor.Collect();
        _lastGamepadCount = s.Gamepads.Count(g => g.IsConnected);
        RefreshPerifericosWith(s);
    }

    private static FontIcon CreatePeripheralTypeIcon(string deviceName)
    {
        var glyph = deviceName.Contains("Headset", StringComparison.OrdinalIgnoreCase) ||
                    deviceName.Contains("Headphone", StringComparison.OrdinalIgnoreCase)
            ? "\uE95B"
            : deviceName.Contains("Mouse", StringComparison.OrdinalIgnoreCase) ||
              deviceName.Contains("Superlight", StringComparison.OrdinalIgnoreCase) ||
              deviceName.Contains("Ergo", StringComparison.OrdinalIgnoreCase)
                ? "\uE962"
                : "\uE961";

        return new FontIcon
        {
            Glyph = glyph,
            FontSize = 18,
            Foreground = new SolidColorBrush(ColorFromHex("#60A5FA")),
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private void UpdateGamepadUi()
    {
        UpdateGamepadState();
    }

    private uint _gpLastPacket = 0;

    private void UpdateGamepadState()
    {
        try
        {
            if (_monitor?.GamepadService == null)
            {
                _gpStatusText.Text = "Serviço indisponível";
                _gpHwCompactStatus.Text = "Serviço indisponível";
                return;
            }
            var gp = _monitor.GamepadService.GetCachedState(0);
            if (gp == null || !gp.IsConnected)
            {
                _gpStatusText.Text = "Nenhum controle conectado";
                _gpHwCompactStatus.Text = "Desconectado";
                return;
            }

            _gpStatusText.Text = $"Conectado — {gp.Name}";
            _gpHwCompactStatus.Text = $"Conectado — {gp.Name}";

            // Analog sticks always update (using cached calibrated centers)
            const double stickRange = 30.0;
            if (_gpLeftStickDot != null)
            {
                var lx = _gpLsCenterX + (gp.ThumbLX / 32767.0) * stickRange;
                var ly = _gpLsCenterY - (gp.ThumbLY / 32767.0) * stickRange;
                Canvas.SetLeft(_gpLeftStickDot, lx - 5);
                Canvas.SetTop(_gpLeftStickDot, ly - 5);
            }
            if (_gpRightStickDot != null)
            {
                var rx = _gpRsCenterX + (gp.ThumbRX / 32767.0) * stickRange;
                var ry = _gpRsCenterY - (gp.ThumbRY / 32767.0) * stickRange;
                Canvas.SetLeft(_gpRightStickDot, rx - 5);
                Canvas.SetTop(_gpRightStickDot, ry - 5);
            }

            // Mini trigger dots in header — follow bezier curve
            if (_gpLtMiniDot != null)
            {
                double ltPct = gp.LeftTrigger / 255.0;
                double ltX = MiniBezier(ltPct, 2.0, 9.0, 22.0, 22.0);
                double ltY = MiniBezier(ltPct, 2.0, 2.0, 14.0, 22.0);
                Canvas.SetLeft(_gpLtMiniDot, ltX - 3);
                Canvas.SetTop(_gpLtMiniDot, ltY - 3);
            }
            if (_gpRtMiniDot != null)
            {
                double rtPct = gp.RightTrigger / 255.0;
                double rtX = MiniBezier(rtPct, 22.0, 15.0, 2.0, 2.0);
                double rtY = MiniBezier(rtPct, 2.0, 2.0, 14.0, 22.0);
                Canvas.SetLeft(_gpRtMiniDot, rtX - 3);
                Canvas.SetTop(_gpRtMiniDot, rtY - 3);
            }

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

internal class FpsToggleTag
{
    public string Key { get; set; } = "";
    public TextBlock Icon { get; set; } = null!;
    public TextBlock Txt { get; set; } = null!;
}
