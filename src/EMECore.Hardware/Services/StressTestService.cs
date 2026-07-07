using System.Diagnostics;
using System.Security.Cryptography;

namespace EMECore.Hardware.Services;

public class StressTestService : IDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _cpuTask;
    private bool _cpuRunning;
    private bool _gpuRunning;
    private Process? _furmarkProcess;
    private string? _furmarkPath;
    private readonly Stopwatch _stopwatch = new();
    private int _cpuThreads;
    private double _cpuLoad;

    public bool IsCpuRunning => _cpuRunning;
    public bool IsGpuRunning => _gpuRunning;
    public bool FurMarkProcessExited => _furmarkProcess != null && _furmarkProcess.HasExited;
    public TimeSpan Elapsed => _stopwatch.Elapsed;
    public int CpuThreads => _cpuThreads;
    public double CpuLoad => _cpuLoad;
    public string? FurMarkPath => _furmarkPath;

    public event Action<double>? CpuLoadUpdated;

    public void StartCpu(int threadCount = 0)
    {
        if (_cpuRunning) return;
        _cts ??= new CancellationTokenSource();
        _cpuRunning = true;
        _cpuThreads = threadCount <= 0 ? Environment.ProcessorCount : threadCount;
        _stopwatch.Restart();

        _cpuTask = Task.Run(() => CpuStressLoop(_cts.Token));
    }

    public void StopCpu()
    {
        _cpuRunning = false;
        _cts?.Cancel();
        _cpuTask?.Wait(3000);
        _cts?.Dispose();
        _cts = null;
        _stopwatch.Stop();
    }

    // ===== GPU Stress Test (FurMark) =====

    public bool DetectFurMark()
    {
        var paths = new[]
        {
            @"C:\Program Files\Geeks3D\FurMark2_x64\furmark.exe",
            @"C:\Program Files (x86)\Geeks3D\FurMark2_x64\furmark.exe",
            @"C:\Program Files\Geeks3D\FurMark_x64\furmark.exe",
            @"C:\Program Files (x86)\Geeks3D\FurMark_x64\furmark.exe",
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                _furmarkPath = path;
                return true;
            }
        }

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (key != null)
            {
                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    using var subKey = key.OpenSubKey(subKeyName);
                    var displayName = subKey?.GetValue("DisplayName")?.ToString() ?? "";
                    var installLocation = subKey?.GetValue("InstallLocation")?.ToString() ?? "";
                    if (displayName.Contains("FurMark", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(installLocation))
                    {
                        var exe = Path.Combine(installLocation, "furmark.exe");
                        if (File.Exists(exe))
                        {
                            _furmarkPath = exe;
                            return true;
                        }
                    }
                }
            }
        }
        catch { }

        return false;
    }

    public void StartGpu(int width = 1280, int height = 720)
    {
        if (_gpuRunning || _furmarkPath == null || !File.Exists(_furmarkPath)) return;

        try
        {
            _furmarkProcess = Process.Start(new ProcessStartInfo
            {
                FileName = _furmarkPath,
                Arguments = $"--demo furmark-vk --width {width} --height {height} --no-osi --max-time 0",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            });

            if (_furmarkProcess != null)
            {
                _gpuRunning = true;
                _stopwatch.Restart();
            }
        }
        catch { }
    }

    public void StopGpu()
    {
        if (!_gpuRunning) return;

        try
        {
            if (_furmarkProcess != null && !_furmarkProcess.HasExited)
            {
                _furmarkProcess.Kill(entireProcessTree: true);
                _furmarkProcess.WaitForExit(3000);
            }
        }
        catch { }

        _furmarkProcess?.Dispose();
        _furmarkProcess = null;
        _gpuRunning = false;
        _stopwatch.Stop();
    }

    public void StopAll()
    {
        StopCpu();
        StopGpu();
    }

    private volatile bool[] _running = Array.Empty<bool>();

    private void CpuStressLoop(CancellationToken ct)
    {
        _running = new bool[_cpuThreads];
        var threads = new Thread[_cpuThreads];

        for (int t = 0; t < _cpuThreads; t++)
        {
            int idx = t;
            threads[t] = new Thread(() =>
            {
                _running[idx] = true;
                try
                {
                    using var sha = SHA256.Create();
                    var data = new byte[1024];
                    var rng = RandomNumberGenerator.Create();
                    rng.GetBytes(data);

                    int iterations = 0;
                    while (!ct.IsCancellationRequested)
                    {
                        var hash = sha.ComputeHash(data);
                        data[0] = hash[0];
                        iterations++;
                        if (iterations % 50 == 0)
                            Thread.Yield();
                    }
                }
                catch { }
                finally { _running[idx] = false; }
            }) { IsBackground = true, Priority = ThreadPriority.BelowNormal };
            threads[t].Start();
        }

        while (!ct.IsCancellationRequested)
        {
            var active = _running.Count(r => r);
            _cpuLoad = _cpuThreads > 0 ? (double)active / _cpuThreads * 100 : 0;
            CpuLoadUpdated?.Invoke(_cpuLoad);
            Thread.Sleep(500);
        }

        foreach (var t in threads) t.Join(2000);
    }

    public void Dispose()
    {
        StopAll();
    }
}
