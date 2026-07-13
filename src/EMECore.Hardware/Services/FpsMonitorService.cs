using System.Diagnostics;
using System.Globalization;

namespace EMECore.Hardware.Services;

public class FpsMonitorService
{
    private readonly List<double> _history = new(120);
    private readonly List<double> _frameTimes = new(120);
    private bool _monitoring;
    private CancellationTokenSource? _cts;
    private string _processName = "";

    public double Current { get; private set; }
    public double Min { get; private set; }
    public double Max { get; private set; }
    public double Avg { get; private set; }
    public double Low1 { get; private set; }
    public double Low01 { get; private set; }
    public double FrameTimeMs { get; private set; }
    public string Source { get; private set; } = "Off";

    public void Start(string processName)
    {
        if (_monitoring) return;
        _monitoring = true;
        _cts = new CancellationTokenSource();
        _processName = processName;
        Source = processName;
        _ = RunLoop(_cts.Token);
    }

    public void Stop()
    {
        _monitoring = false;
        _cts?.Cancel();
        _history.Clear();
        _frameTimes.Clear();
        Current = Min = Max = Avg = Low1 = Low01 = FrameTimeMs = 0;
        Source = "Off";
        _processName = "";
    }

    private async Task RunLoop(CancellationToken ct)
    {
        var toolsDir = FindToolsDir();
        var pmPath = toolsDir != null ? Path.Combine(toolsDir, "PresentMon-2.5.1-x64.exe") : null;

        Log($"PM path: {pmPath ?? "NULL"}, exists: {pmPath != null && File.Exists(pmPath)}");

        if (pmPath == null || !File.Exists(pmPath))
        {
            Source = "PresentMon not found";
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var csvPath = Path.Combine(Path.GetTempPath(), $"eme-fps-{Guid.NewGuid():N}.csv");

                var args = $"--output_file \"{csvPath}\" --no_console_stats --session_name EMEFPS --stop_existing_session";

                var psi = new ProcessStartInfo(pmPath, args)
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true
                };
                var p = Process.Start(psi);
                if (p != null)
                {
                    await Task.Delay(2200, ct);
                    try { p.Kill(true); } catch { }
                    await Task.Delay(300, ct);
                    ParseCsv(csvPath);
                    try { File.Delete(csvPath); } catch { }
                }
                await Task.Delay(200, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Log($"RunLoop error: {ex.Message}"); await Task.Delay(500, ct); }
        }
    }

    private void ParseCsv(string path)
    {
        if (!File.Exists(path)) { return; }
        try
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length < 2) return;

            var headers = lines[0].Split(',');
            int msIdx = -1;
            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i].Trim() == "MsBetweenPresents") msIdx = i;
            }
            if (msIdx < 0) return;

            for (int i = 1; i < lines.Length; i++)
            {
                var cols = lines[i].Split(',');
                if (cols.Length > msIdx && double.TryParse(cols[msIdx].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ms) && ms > 0 && ms < 5000)
                {
                    var fps = Math.Round(1000.0 / ms);
                    if (fps > 0 && fps < 500)
                    {
                        _history.Add(fps);
                        if (_history.Count > 120) _history.RemoveAt(0);
                        _frameTimes.Add(ms);
                        if (_frameTimes.Count > 120) _frameTimes.RemoveAt(0);
                    }
                }
            }

            if (_history.Count > 0)
            {
                Current = _history.Last();
                Min = _history.Min();
                Max = _history.Max();
                Avg = Math.Round(_history.Average());
                FrameTimeMs = Math.Round(1000.0 / Current, 2);

                var sorted = _frameTimes.OrderByDescending(x => x).ToList();
                int count1 = Math.Max(1, (int)Math.Ceiling(sorted.Count * 0.01));
                int count01 = Math.Max(1, (int)Math.Ceiling(sorted.Count * 0.001));
                Low1 = Math.Round(1000.0 / sorted.Take(count1).Average(), 1);
                Low01 = Math.Round(1000.0 / sorted.Take(count01).Average(), 1);
            }
        }
        catch { }
    }

    private static void Log(string msg) { }

    private static string? FindToolsDir()
    {
        var dir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var p = Path.Combine(dir, "tools");
            if (Directory.Exists(p)) return p;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        return null;
    }
}
