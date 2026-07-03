using System.Diagnostics;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class PlayTimeTrackerService : IDisposable
{
    private readonly DatabaseService _databaseService;
    private readonly Dictionary<string, DateTime> _sessionStartTimes = new();
    private readonly Dictionary<string, int> _lastKnownPlayTimes = new();
    private readonly System.Threading.Timer _checkTimer;
    private bool _disposed;

    public PlayTimeTrackerService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _checkTimer = new System.Threading.Timer(CheckRunningProcesses, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public void StartTracking(Game game)
    {
        if (string.IsNullOrEmpty(game.ExecutablePath)) return;

        var processName = Path.GetFileNameWithoutExtension(game.ExecutablePath);
        if (!_sessionStartTimes.ContainsKey(processName))
        {
            _sessionStartTimes[processName] = DateTime.UtcNow;
            _lastKnownPlayTimes[processName] = game.PlayTime;
        }
    }

    public void StopTracking(Game game)
    {
        if (string.IsNullOrEmpty(game.ExecutablePath)) return;

        var processName = Path.GetFileNameWithoutExtension(game.ExecutablePath);
        if (_sessionStartTimes.TryGetValue(processName, out var startTime))
        {
            var duration = (int)(DateTime.UtcNow - startTime).TotalMinutes;
            if (duration > 0)
            {
                var newPlayTime = (_lastKnownPlayTimes.GetValueOrDefault(processName, 0)) + duration;
                _ = _databaseService.UpdateGamePlayTimeAsync(game.Id, newPlayTime, null);
                game.PlayTime = newPlayTime;
            }
            _sessionStartTimes.Remove(processName);
            _lastKnownPlayTimes.Remove(processName);
        }
    }

    private async void CheckRunningProcesses(object? state)
    {
        try
        {
            foreach (var entry in _sessionStartTimes.ToList())
            {
                var processes = Process.GetProcessesByName(entry.Key);
                if (processes.Length == 0)
                {
                    var duration = (int)(DateTime.UtcNow - entry.Value).TotalMinutes;
                    if (duration > 0)
                    {
                        var game = await FindGameByProcessName(entry.Key);
                        if (game != null)
                        {
                            var newPlayTime = (_lastKnownPlayTimes.GetValueOrDefault(entry.Key, 0)) + duration;
                            await _databaseService.UpdateGamePlayTimeAsync(game.Id, newPlayTime, DateTime.UtcNow);
                            game.PlayTime = newPlayTime;
                            game.LastPlayed = DateTime.UtcNow;
                        }
                    }
                    _sessionStartTimes.Remove(entry.Key);
                    _lastKnownPlayTimes.Remove(entry.Key);
                }
            }
        }
        catch { }
    }

    private async Task<Game?> FindGameByProcessName(string processName)
    {
        var games = await _databaseService.GetGamesAsync();
        return games.FirstOrDefault(g => 
            !string.IsNullOrEmpty(g.ExecutablePath) && 
            Path.GetFileNameWithoutExtension(g.ExecutablePath).Equals(processName, StringComparison.OrdinalIgnoreCase));
    }

    public int GetCurrentSessionMinutes(string processName)
    {
        if (_sessionStartTimes.TryGetValue(processName, out var startTime))
        {
            return (int)(DateTime.UtcNow - startTime).TotalMinutes;
        }
        return 0;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _checkTimer.Dispose();
            _disposed = true;
        }
    }
}
