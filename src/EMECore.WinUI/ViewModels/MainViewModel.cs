using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EMECore.Core.Models;
using EMECore.Core.Services;
using EMECore.Core.Helpers;
using EMECore.Hardware.Services;

namespace EMECore.WinUI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDatabaseService _databaseService;
    private readonly IGameScannerService _gameScannerService;
    private readonly ISteamStoreService _steamStoreService;
    private readonly PlayTimeTrackerService _playTimeTracker;
    private static readonly SemaphoreSlim _steamApiSemaphore = new(3);

    [ObservableProperty] private string _currentPage = "library";
    [ObservableProperty] private Game? _selectedGame;
    [ObservableProperty] private bool _isSidebarVisible = true;
    [ObservableProperty] private string _statusText = "Pronto";
    [ObservableProperty] private int _totalGames;
    [ObservableProperty] private string _totalPlayTime = "0m";
    [ObservableProperty] private bool _isScanning;

    public ObservableCollection<Game> Games { get; } = new();

    public MainViewModel(IDatabaseService databaseService, IGameScannerService gameScannerService, ISteamStoreService steamStoreService)
    {
        _databaseService = databaseService;
        _gameScannerService = gameScannerService;
        _steamStoreService = steamStoreService;
        _playTimeTracker = new PlayTimeTrackerService((DatabaseService)databaseService);
    }

    public async Task InitializeAsync(string dbPath)
    {
        await _databaseService.InitializeAsync(dbPath);
        await LoadGamesAsync();
    }

    [RelayCommand]
    private void NavigateTo(string page)
    {
        CurrentPage = page;
    }

    [RelayCommand]
    private void SelectGame(Game? game)
    {
        SelectedGame = game;
        if (game != null)
            CurrentPage = "detail";
    }

    [RelayCommand]
    private void GoBack()
    {
        SelectedGame = null;
        CurrentPage = "library";
    }

    [RelayCommand]
    private void AddGame()
    {
        CurrentPage = "addgame";
    }

    private async Task LoadGamesAsync()
    {
        var games = await _databaseService.GetGamesAsync();
        Games.Clear();
        foreach (var game in games)
            Games.Add(game);

        TotalGames = Games.Count;
        var totalTime = await _databaseService.GetTotalPlayTimeAsync();
        TotalPlayTime = FormatHelpers.FormatMinutes(totalTime);
        StatusText = $"{TotalGames} jogos carregados";
    }

    [RelayCommand]
    private async Task ScanGamesAsync()
    {
        if (IsScanning) return;
        IsScanning = true;
        StatusText = "Procurando jogos...";

        try
        {
            var scanned = await _gameScannerService.ScanAllGamesAsync();
            int added = 0;
            foreach (var s in scanned)
            {
                if (!Games.Any(g => g.ExecutablePath == s.ExecutablePath))
                {
                    var game = new Game
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = s.Name,
                        ExecutablePath = s.ExecutablePath,
                        Platform = s.Platform,
                        SteamAppId = s.SteamAppId,
                        CoverImage = s.CoverImage,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _databaseService.UpsertGameAsync(game);
                    Games.Add(game);
                    added++;
                }
            }
            TotalGames = Games.Count;
            StatusText = added > 0 ? $"{added} jogos novos encontrados" : "Nenhum jogo novo encontrado";

            await RefreshMissingCoversAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Erro: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task RefreshMissingCoversAsync()
    {
        var gamesWithoutCover = Games
            .Where(g => !string.IsNullOrEmpty(g.SteamAppId) && string.IsNullOrEmpty(g.CoverImage))
            .ToList();

        if (gamesWithoutCover.Count == 0) return;

        StatusText = $"Baixando {gamesWithoutCover.Count} capas...";

        var tasks = gamesWithoutCover.Select(async game =>
        {
            await _steamApiSemaphore.WaitAsync();
            try
            {
                var info = await _steamStoreService.GetStoreInfoAsync(game.SteamAppId);
                if (info != null && !string.IsNullOrEmpty(info.HeaderImage))
                {
                    game.CoverImage = info.HeaderImage;
                    await _databaseService.UpsertGameAsync(game);
                }
            }
            catch { }
            finally
            {
                _steamApiSemaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        StatusText = $"{TotalGames} jogos carregados";
    }

    [RelayCommand]
    private async Task DeleteGameAsync(Game? game)
    {
        if (game == null) return;
        await _databaseService.DeleteGameAsync(game.Id);
        Games.Remove(game);
        TotalGames = Games.Count;
        if (SelectedGame?.Id == game.Id)
        {
            SelectedGame = null;
            CurrentPage = "library";
        }
        StatusText = $"\"{game.Name}\" removido";
    }

    [RelayCommand]
    private async Task LaunchGameAsync(Game? game)
    {
        if (game == null || string.IsNullOrEmpty(game.ExecutablePath)) return;

        StatusText = $"Abrindo {game.Name}...";

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = game.ExecutablePath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            game.LastPlayed = DateTime.UtcNow;
            game.LastSessionStart = DateTime.UtcNow;
            await _databaseService.UpsertGameAsync(game);
            
            _playTimeTracker.StartTracking(game);
            
            StatusText = $"{game.Name} iniciado";
        }
        catch (Exception ex)
        {
            StatusText = $"Erro ao abrir: {ex.Message}";
        }
    }

    public async Task AddGameManualAsync(string name, string exePath, string platform)
    {
        var game = new Game
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            ExecutablePath = exePath,
            Platform = platform,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _databaseService.UpsertGameAsync(game);
        Games.Add(game);
        TotalGames = Games.Count;
        StatusText = $"\"{name}\" adicionado";
        CurrentPage = "library";
    }
}
