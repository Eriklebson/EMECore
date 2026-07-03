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
    private readonly LibraryPage _libraryPage;
    private readonly GameDetailPage _detailPage;
    private readonly AddGamePage _addGamePage;
    private readonly AchievementService _achievementService;
    private List<Achievement>? _lastAchievements;

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

        var rootGrid = new Grid { Background = new SolidColorBrush(SteamColors.Dark) };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleBar = new Grid { Background = new SolidColorBrush(SteamColors.Darkest) };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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

        var windowButtons = new StackPanel { Orientation = Orientation.Horizontal };
        windowButtons.Children.Add(CreateTitleBarButton("\uE921", MinimizeButton_Click));
        windowButtons.Children.Add(CreateTitleBarButton("\uE922", MaximizeButton_Click));
        windowButtons.Children.Add(CreateTitleBarButton("\uE8BB", CloseButton_Click));
        Grid.SetColumn(windowButtons, 2);
        titleBar.Children.Add(windowButtons);

        Grid.SetRow(titleBar, 0);
        rootGrid.Children.Add(titleBar);

        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _sidebar = new Sidebar { Background = SteamColors.DarkerBrush };
        _sidebar.NavigationRequested += Sidebar_NavigationRequested;
        _sidebar.MonitorRequested += Sidebar_MonitorRequested;
        _sidebar.FishingMacroRequested += Sidebar_FishingMacroRequested;
        contentGrid.Children.Add(_sidebar);

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
        _detailPage.TestAchievementRequested += DetailPage_TestAchievementRequested;
        pageContainer.Children.Add(_detailPage);

        _addGamePage = new AddGamePage { Visibility = Visibility.Collapsed };
        _addGamePage.GameAdded += AddGamePage_GameAdded;
        _addGamePage.CancelRequested += AddGamePage_CancelRequested;
        pageContainer.Children.Add(_addGamePage);

        Grid.SetColumn(pageContainer, 1);
        contentGrid.Children.Add(pageContainer);

        Grid.SetRow(contentGrid, 1);
        rootGrid.Children.Add(contentGrid);

        Content = rootGrid;

        this.Activated += MainWindow_Activated;
    }

    private static Button CreateTitleBarButton(string glyph, RoutedEventHandler handler)
    {
        var btn = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontSize = 10 },
            Width = 40,
            Height = 40,
            Background = new SolidColorBrush(Colors.Transparent),
            Foreground = SteamColors.TextBrush,
            BorderThickness = new Thickness(0)
        };
        btn.Click += handler;
        return btn;
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated)
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
                    });
                }
            };
        }
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

        if (page == "detail" && ViewModel.SelectedGame != null)
        {
            _detailPage.LoadGame(ViewModel.SelectedGame);
            var achievements = await _achievementService.GetAchievementsAsync(ViewModel.SelectedGame);
            _detailPage.SetAchievements(achievements);
            
            // Verificar novas conquistas desbloqueadas
            if (_lastAchievements != null)
            {
                var newAchievements = achievements.Where(a => a.Achieved && 
                    !_lastAchievements.Any(old => old.Apiname == a.Apiname && old.Achieved)).ToList();
                
                foreach (var ach in newAchievements.Take(3)) // Máximo 3 popups
                {
                    var notification = new AchievementNotificationWindow();
                    notification.Show(ach);
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

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        ShowWindow(hwnd, 6);
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            if (presenter.State == OverlappedPresenterState.Maximized)
                presenter.Restore();
            else
                presenter.Maximize();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void Sidebar_NavigationRequested(object? sender, string page)
    {
        ViewModel.NavigateToCommand.Execute(page);
    }

    private void Sidebar_MonitorRequested(object? sender, EventArgs e)
    {
        if (!IsRunningAsAdmin())
        {
            var exePath = Environment.ProcessPath ?? "";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--monitor",
                UseShellExecute = true,
                Verb = "runas"
            });
            return;
        }
        new MonitorWindow().Activate();
    }

    private static bool IsRunningAsAdmin()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private void Sidebar_FishingMacroRequested(object? sender, EventArgs e)
    {
        new FishingMacroWindow().Activate();
    }

    private async void LibraryPage_ScanRequested(object? sender, EventArgs e)
    {
        await ViewModel.ScanGamesCommand.ExecuteAsync(null);
        _libraryPage.LoadGames(ViewModel.Games);
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

    private void DetailPage_TestAchievementRequested(object? sender, EventArgs e)
    {
        var testAchievement = new Achievement
        {
            Apiname = "Test_Achievement",
            Name = "Protocolo EVE",
            Description = "Adquira todos os troféus",
            Achieved = true
        };
        var notification = new AchievementNotificationWindow();
        notification.Show(testAchievement);
    }
}
