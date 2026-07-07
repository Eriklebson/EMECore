using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class SaveMonitorService : IDisposable
{
    private readonly StellarBladeParser _parser;
    private FileSystemWatcher? _watcher;
    private Dictionary<string, bool> _previousState = new();
    private bool _isMonitoring;
    private DateTime _lastParseTime = DateTime.MinValue;
    private readonly object _lock = new();

    public event Action<Achievement>? OnAchievementUnlocked;
    public event Action<StellarBladeSaveData>? OnSaveUpdated;
    public bool IsMonitoring => _isMonitoring;

    public SaveMonitorService()
    {
        _parser = new StellarBladeParser();
    }

    public bool StartMonitoring()
    {
        if (_isMonitoring) return true;

        var savePath = _parser.FindSavePath();
        if (savePath == null) return false;

        try
        {
            var dir = Path.GetDirectoryName(savePath);
            if (dir == null || !Directory.Exists(dir)) return false;

            TakeSnapshot();

            _watcher = new FileSystemWatcher(dir, "StellarBladeSave00.sav")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnSaveFileChanged;
            _isMonitoring = true;

            System.Diagnostics.Debug.WriteLine($"[SaveMonitor] Iniciado: {savePath}");
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
                var saveData = _parser.ParseSave();
                if (saveData == null) return;

                OnSaveUpdated?.Invoke(saveData);

                var currentAchievements = _parser.ParseAchievements();
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
            var achievements = _parser.ParseAchievements();
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
            OnAchievementUnlocked?.Invoke(achievement);
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
