using System.Diagnostics;
using System.Globalization;

namespace EMECore.Hardware.Services;

public class FpsMonitorService
{
    private readonly List<double> _history = new(120);
    private bool _monitoring;
    private CancellationTokenSource? _cts;

    public double Current { get; private set; }
    public double Min { get; private set; }
    public double Max { get; private set; }
    public double Avg { get; private set; }
    public string Source { get; private set; } = "Off";

    public void Start(string processName)
    {
        if (_monitoring) return;
        _monitoring = true;
        _cts = new CancellationTokenSource();
        Source = processName;
        _ = RunLoop(_cts.Token);
    }

    public void Stop()
    {
        _monitoring = false;
        _cts?.Cancel();
        _history.Clear();
        Current = Min = Max = Avg = 0;
        Source = "Off";
    }

    private async Task RunLoop(CancellationToken ct)
    {
        var csvPath = Path.Combine(Path.GetTempPath(), "eme-fps.csv");
        var toolsDir = FindToolsDir();
        var pmPath = toolsDir != null ? Path.Combine(toolsDir, "PresentMon-2.5.1-x64.exe") : null;

        if (pmPath == null || !File.Exists(pmPath))
        {
            Source = "PresentMon not found";
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (File.Exists(csvPath)) File.Delete(csvPath);

                var psi = new ProcessStartInfo(pmPath, $"--output_file \"{csvPath}\" --no_console_stats --session_name EMEFPS --stop_existing_session")
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true
                };
                var p = Process.Start(psi);
                if (p != null)
                {
                    await Task.Delay(2200, ct);
                    try { p.Kill(true); } catch { }
                    await Task.Delay(300, ct);
                    ParseCsv(csvPath, Source);
                }
                await Task.Delay(200, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(500, ct); }
        }
    }

    private void ParseCsv(string path, string _)
    {
        if (!File.Exists(path)) return;
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
                    }
                }
            }

            if (_history.Count > 0)
            {
                Current = _history.Last();
                Min = _history.Min();
                Max = _history.Max();
                Avg = Math.Round(_history.Average());
            }
        }
        catch { }
    }

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
