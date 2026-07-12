using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class SaveMonitorService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private Dictionary<string, bool> _previousState = new();
    private bool _isMonitoring;
    private DateTime _lastParseTime = DateTime.MinValue;
    private readonly object _lock = new();

    private Func<List<Achievement>>? _parseFunc;
    private string _gameName = "";

    public event Action<Achievement, string>? OnAchievementUnlocked;
    public bool IsMonitoring => _isMonitoring;

    public bool StartMonitoring(string savePath, Func<List<Achievement>> parseFunc, string gameName)
    {
        StopMonitoring();

        if (string.IsNullOrEmpty(savePath) || !File.Exists(savePath)) return false;

        try
        {
            var dir = Path.GetDirectoryName(savePath);
            if (dir == null || !Directory.Exists(dir)) return false;

            var fileName = Path.GetFileName(savePath);

            _parseFunc = parseFunc;
            _gameName = gameName;

            TakeSnapshot();

            _watcher = new FileSystemWatcher(dir, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnSaveFileChanged;
            _isMonitoring = true;

            System.Diagnostics.Debug.WriteLine($"[SaveMonitor] Iniciado: {gameName} → {savePath}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SaveMonitor] Erro ao iniciar: {ex.Message}");
            return false;
        }
    }

    public void StopMonitoring()
    {
        if (_watcher != null)
        {
            _watcher.Changed -= OnSaveFileChanged;
            _watcher.Dispose();
            _watcher = null;
        }

        _isMonitoring = false;
        _previousState.Clear();
        _parseFunc = null;
        _gameName = "";
        System.Diagnostics.Debug.WriteLine("[SaveMonitor] Parado");
    }

    private void OnSaveFileChanged(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastParseTime).TotalMilliseconds < 500) return;
            _lastParseTime = now;

            Thread.Sleep(100);

            try
            {
                if (_parseFunc == null) return;

                var currentAchievements = _parseFunc();
                DetectNewAchievements(currentAchievements);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SaveMonitor] Erro no parse: {ex.Message}");
            }
        }
    }

    private void TakeSnapshot()
    {
        _previousState.Clear();

        try
        {
            if (_parseFunc == null) return;

            var achievements = _parseFunc();
            foreach (var a in achievements)
            {
                _previousState[a.Apiname] = a.Achieved;
            }

            System.Diagnostics.Debug.WriteLine($"[SaveMonitor] Snapshot: {_previousState.Count} conquistas, {_previousState.Values.Count(v => v)} desbloqueadas");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SaveMonitor] Erro no snapshot: {ex.Message}");
        }
    }

    private void DetectNewAchievements(List<Achievement> currentAchievements)
    {
        foreach (var achievement in currentAchievements)
        {
            if (!achievement.Achieved) continue;

            if (_previousState.TryGetValue(achievement.Apiname, out var wasAchieved) && wasAchieved)
                continue;

            System.Diagnostics.Debug.WriteLine($"[SaveMonitor] NOVA CONQUISTA: {achievement.Name}");
            OnAchievementUnlocked?.Invoke(achievement, _gameName);
            _previousState[achievement.Apiname] = true;
        }
    }

    public void ResetSnapshot()
    {
        TakeSnapshot();
    }

    public void Dispose()
    {
        StopMonitoring();
        GC.SuppressFinalize(this);
    }
}
