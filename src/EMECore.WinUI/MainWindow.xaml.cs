using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using EMECore.Core.Models;
using EMECore.Core.Services;
using EMECore.Hardware.Services;
using EMECore.WinUI.ViewModels;
using EMECore.WinUI.Views;
using EMECore.WinUI.Theme;

namespace EMECore.WinUI;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    private readonly Grid _dragRegion;
    private readonly Sidebar _sidebar;
    private readonly ColumnDefinition _sidebarColumn;
    private readonly LibraryPage _libraryPage;
    private readonly GameDetailPage _detailPage;
    private readonly AddGamePage _addGamePage;
    private readonly AchievementService _achievementService;
    private readonly SaveMonitorService _saveMonitor;
    private readonly SaveDiscoveryService _saveDiscovery;
    private readonly SaveParserService _saveParser;
    private readonly SaveBasedAchievementProvider _saveAchievementProvider;
    private readonly AchievementCheckerService _achievementChecker;
    private readonly AchievementsPage _achievementsPage;

    private List<Achievement>? _lastAchievements;
    private MonitorWindow? _monitorWindow;

    public MainWindow()
    {
        this.Title = "E.M.E Core";
        
        // Definir ícone da janela
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon("Assets/Logo/logo.ico");

        var databaseService = new DatabaseService();
        var steamStoreService = new SteamStoreService();
        var gameScannerService = new GameScannerService(steamStoreService);
        ViewModel = new MainViewModel(databaseService, gameScannerService, steamStoreService);
        _achievementService = new AchievementService();
        _saveMonitor = new SaveMonitorService();
        _saveDiscovery = new SaveDiscoveryService();
        _saveParser = new SaveParserService();
        _saveAchievementProvider = new SaveBasedAchievementProvider(_saveDiscovery, _saveParser);
        _achievementChecker = new AchievementCheckerService(_saveAchievementProvider);

        var rootGrid = new Grid { Background = new SolidColorBrush(SteamColors.Dark) };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleBar = new Grid { Background = new SolidColorBrush(SteamColors.Darkest) };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var logoPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
        
        // Logo do projeto
        var logoImage = new Microsoft.UI.Xaml.Controls.Image
        {
            Width = 24,
            Height = 24,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var logoBitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/Logo/logo.png"));
        logoImage.Source = logoBitmap;
        logoPanel.Children.Add(logoImage);
        
        logoPanel.Children.Add(new TextBlock { Text = "E.M.E Core", FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = SteamColors.TextBrush, VerticalAlignment = VerticalAlignment.Center });
        titleBar.Children.Add(logoPanel);

        _dragRegion = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
        Grid.SetColumn(_dragRegion, 1);
        titleBar.Children.Add(_dragRegion);

        Grid.SetRow(titleBar, 0);
        rootGrid.Children.Add(titleBar);

        var contentGrid = new Grid();
        _sidebarColumn = new ColumnDefinition { Width = new GridLength(220) };
        contentGrid.ColumnDefinitions.Add(_sidebarColumn);
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _sidebar = new Sidebar { Background = SteamColors.DarkerBrush };
        _sidebar.NavigationRequested += Sidebar_NavigationRequested;
        _sidebar.MonitorRequested += Sidebar_MonitorRequested;
        _sidebar.TestAchievementRequested += Sidebar_TestAchievementRequested;
        _sidebar.CollapseChanged += Sidebar_CollapseChanged;
        contentGrid.Children.Add(_sidebar);

        SettingsService.Load();
        var lastCategory = SettingsService.Get("sidebar_category", "library");
        _sidebar.SetActiveCategory(lastCategory);

        var pageContainer = new Grid();
        _libraryPage = new LibraryPage { Visibility = Visibility.Visible };
        _libraryPage.GameSelected += LibraryPage_GameSelected;
        _libraryPage.GameLaunchRequested += LibraryPage_GameLaunchRequested;
        _libraryPage.ScanRequested += LibraryPage_ScanRequested;
        pageContainer.Children.Add(_libraryPage);

        _detailPage = new GameDetailPage { Visibility = Visibility.Collapsed };
        _detailPage.BackRequested += DetailPage_BackRequested;
        _detailPage.LaunchRequested += DetailPage_LaunchRequested;
        _detailPage.DeleteRequested += DetailPage_DeleteRequested;
        pageContainer.Children.Add(_detailPage);

        _addGamePage = new AddGamePage { Visibility = Visibility.Collapsed };
        _addGamePage.GameAdded += AddGamePage_GameAdded;
        _addGamePage.CancelRequested += AddGamePage_CancelRequested;
        pageContainer.Children.Add(_addGamePage);

        _achievementsPage = new AchievementsPage(_achievementChecker) { Visibility = Visibility.Collapsed };
        pageContainer.Children.Add(_achievementsPage);

        Grid.SetColumn(pageContainer, 1);
        contentGrid.Children.Add(pageContainer);

        Grid.SetRow(contentGrid, 1);
        rootGrid.Children.Add(contentGrid);

        Content = rootGrid;

        Closed += (_, _) =>
        {
            _saveMonitor.Dispose();
            _monitorWindow?.Close();
            try { ViewModel.CloseDatabaseSync(); } catch { }
            try
            {
                var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EMECore", "eme_core.db");
                System.IO.File.Delete(dbPath + "-shm");
                System.IO.File.Delete(dbPath + "-wal");
            }
            catch { }
            Environment.Exit(0);
        };

        this.Activated += MainWindow_Activated;
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        this.Activated -= MainWindow_Activated;

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1400, Height = 900 });

        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        var titleBarObj = appWindow.TitleBar;
        titleBarObj.BackgroundColor = ParseColor("#0e1621");
        titleBarObj.ButtonBackgroundColor = ParseColor("#0e1621");
        titleBarObj.ButtonHoverBackgroundColor = ParseColor("#1b2838");
        titleBarObj.ButtonPressedBackgroundColor = ParseColor("#2a475e");
        
        SetTitleBar(_dragRegion);

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EMECore", "eme_core.db");
        await ViewModel.InitializeAsync(dbPath);

        _libraryPage.LoadGames(ViewModel.Games);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        _sidebar.UpdateStats(
            $"{ViewModel.TotalGames} jogos",
            $"{ViewModel.TotalPlayTime} jogado",
            ViewModel.StatusText);

        var savedCategory = SettingsService.Get("sidebar_category", "library");
        if (savedCategory != "library")
            Sidebar_NavigationRequested(this, savedCategory);

        var savedCollapsed = SettingsService.Get("sidebar_collapsed", "False");
        if (savedCollapsed == "True")
        {
            _sidebar.SetCollapsed(true);
            _sidebarColumn.Width = new GridLength(64);
        }

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.StatusText))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _sidebar.UpdateStats(
                        $"{ViewModel.TotalGames} jogos",
                        $"{ViewModel.TotalPlayTime} jogado",
                        ViewModel.StatusText);
                    _libraryPage.SetScanning(ViewModel.IsScanning);
                    _libraryPage.LoadGames(ViewModel.Games);
                });
            }
        };

        _saveMonitor.OnAchievementUnlocked += OnAchievementUnlocked;
        _saveMonitor.StartMonitoring();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
            UpdatePageVisibility();
        else if (e.PropertyName == nameof(MainViewModel.Games))
            _libraryPage.LoadGames(ViewModel.Games);
    }

    private async void UpdatePageVisibility()
    {
        var page = ViewModel.CurrentPage;
        _libraryPage.Visibility = page == "library" ? Visibility.Visible : Visibility.Collapsed;
        _detailPage.Visibility = page == "detail" ? Visibility.Visible : Visibility.Collapsed;
        _addGamePage.Visibility = page == "addgame" ? Visibility.Visible : Visibility.Collapsed;
        _achievementsPage.Visibility = page == "achievements" ? Visibility.Visible : Visibility.Collapsed;

        if (page == "achievements")
        {
            _achievementsPage.LoadAchievements(ViewModel.Games.ToList());
        }

        if (page == "detail" && ViewModel.SelectedGame != null)
        {
            _detailPage.LoadGame(ViewModel.SelectedGame);
            
            // Buscar SteamAppId ANTES de carregar conquistas
            if (string.IsNullOrEmpty(ViewModel.SelectedGame.SteamAppId))
            {
                var steamStoreService = new SteamStoreService();
                var appId = await steamStoreService.SearchAppIdAsync(ViewModel.SelectedGame.Name);
                if (!string.IsNullOrEmpty(appId))
                    ViewModel.SelectedGame.SteamAppId = appId;
            }

            var achievements = await _achievementService.GetAchievementsAsync(ViewModel.SelectedGame);
            await _detailPage.SetAchievements(achievements);
            
            // Carregar requisitos do jogo
            if (!string.IsNullOrEmpty(ViewModel.SelectedGame.SteamAppId))
            {
                var steamStoreService = new SteamStoreService();
                var storeInfo = await steamStoreService.GetStoreInfoAsync(ViewModel.SelectedGame.SteamAppId);
                _detailPage.SetRequirements(storeInfo?.Requirements, ViewModel.SelectedGame.Platform);
            }
            else
            {
                _detailPage.SetRequirements(null, ViewModel.SelectedGame.Platform);
            }
            
            // Verificar novas conquistas desbloqueadas
            if (_lastAchievements != null)
            {
                var newAchievements = achievements.Where(a => a.Achieved && 
                    !_lastAchievements.Any(old => old.Apiname == a.Apiname && old.Achieved)).ToList();
                
                foreach (var ach in newAchievements.Take(3)) // Máximo 3 popups
                {
                    var notification = new AchievementNotificationWindow();
                    notification.Show(ach, ViewModel.SelectedGame?.Name ?? "");
                    await Task.Delay(500); // Delay entre popups
                }
            }
            _lastAchievements = achievements;
        }
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        return Windows.UI.Color.FromArgb(255,
            byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber));
    }

    private void Sidebar_NavigationRequested(object? sender, string page)
    {
        SettingsService.Set("sidebar_category", page);

        if (page == "library" || page == "tools" || page == "training")
        {
            var category = page == "library" ? "game" : page == "tools" ? "tool" : "training";
            _libraryPage.SetCategory(category);
            ViewModel.CurrentPage = "library";
        }
        else
        {
            ViewModel.NavigateToCommand.Execute(page);
        }
    }

    private void Sidebar_CollapseChanged(object? sender, bool isCollapsed)
    {
        var targetWidth = isCollapsed ? 64.0 : 220.0;
        _sidebarColumn.Width = new GridLength(targetWidth);
    }

    private void Sidebar_MonitorRequested(object? sender, EventArgs e)
    {
        if (_monitorWindow == null)
        {
            _monitorWindow = new MonitorWindow();
            _monitorWindow.Closed += (_, _) => _monitorWindow = null;

            var ownerHwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var childHwnd = WinRT.Interop.WindowNative.GetWindowHandle(_monitorWindow);
            SetWindowLongPtrW(childHwnd, GWL_HWNDPARENT, ownerHwnd);
        }
        _monitorWindow.Activate();
    }

    private void Sidebar_TestAchievementRequested(object? sender, EventArgs e)
    {
        var testAchievement = new Achievement
        {
            Apiname = "Test_Achievement",
            Name = "Protocolo EVE",
            Description = "Adquira todos os troféus",
            Achieved = true
        };
        var notification = new AchievementNotificationWindow();
        notification.Show(testAchievement, "Stellar Blade");
    }

    private void OnAchievementUnlocked(Achievement achievement)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            var notification = new AchievementNotificationWindow();
            notification.Show(achievement, "Stellar Blade");
            await Task.Delay(500);
        });
    }

    private async void LibraryPage_ScanRequested(object? sender, EventArgs e)
    {
        await ViewModel.ScanGamesCommand.ExecuteAsync(null);
    }

    private void LibraryPage_GameSelected(object? sender, Game game)
    {
        ViewModel.SelectGameCommand.Execute(game);
    }

    private async void LibraryPage_GameLaunchRequested(object? sender, Game game)
    {
        await ViewModel.LaunchGameCommand.ExecuteAsync(game);
    }

    private void DetailPage_BackRequested(object? sender, EventArgs e)
    {
        ViewModel.GoBackCommand.Execute(null);
    }

    private async void DetailPage_LaunchRequested(object? sender, Game game)
    {
        await ViewModel.LaunchGameCommand.ExecuteAsync(game);
    }

    private async void DetailPage_DeleteRequested(object? sender, Game game)
    {
        await ViewModel.DeleteGameCommand.ExecuteAsync(game);
        _libraryPage.LoadGames(ViewModel.Games);
    }

    private async void AddGamePage_GameAdded(object? sender, EventArgs e)
    {
        var (name, exePath, platform) = _addGamePage.GetFormData();
        await ViewModel.AddGameManualAsync(name, exePath, platform);
        _addGamePage.ClearForm();
        _libraryPage.LoadGames(ViewModel.Games);
    }

    private void AddGamePage_CancelRequested(object? sender, EventArgs e)
    {
        _addGamePage.ClearForm();
        ViewModel.NavigateToCommand.Execute("library");
    }

    private const int GWL_HWNDPARENT = -8;

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);
}
