using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using EMECore.Core.Models;
using LibreHardwareMonitor.Hardware;

namespace EMECore.Hardware.Services;

public class HardwareMonitorService
{
    private readonly FpsMonitorService _fpsMonitor = new();
    private string? _lastDetectedGame;
    private DateTime _gameDetectCache = DateTime.MinValue;
    private List<FanInfo> _cachedFans = new();
    private DateTime _fansCache = DateTime.MinValue;

    // Network delta tracking
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastNetworkCheck = DateTime.MinValue;

    // Cache file
    private static readonly string CacheFile = Path.Combine(Path.GetTempPath(), "eme_hw_cache.json");
    private JsonDocument? _cacheDoc;
    private DateTime _lastCacheRead = DateTime.MinValue;

    public HardwareMonitorService()
    {
        // Start background collector
        StartCollector();
    }

    private void StartCollector()
    {
        // Start data collector (runs continuously)
        var sp = FindToolsFile("hw-collector.ps1");
        if (sp != null)
        {
            Log($"Starting hw-collector.ps1 from {sp}");
            Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo("powershell.exe")
                    {
                        Arguments = $"-ExecutionPolicy Bypass -File \"{sp}\"",
                        UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardOutput = true, RedirectStandardError = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex) { Log($"hw-collector error: {ex.Message}"); }
            });
        }
        else { Log("hw-collector.ps1 NOT FOUND"); }

        // Start temperature collector (runs periodically, needs admin)
        var ts = FindToolsFile("check-cpu-temp.ps1");
        if (ts != null && IsRunningAsAdmin())
        {
            Log($"Starting temp collector from {ts}");
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var psi = new ProcessStartInfo("powershell.exe")
                        {
                            Arguments = $"-ExecutionPolicy Bypass -File \"{ts}\"",
                            UseShellExecute = false, CreateNoWindow = true,
                            RedirectStandardOutput = true, RedirectStandardError = true
                        };
                        var p = Process.Start(psi);
                        if (p != null) await p.WaitForExitAsync();
                    }
                    catch (Exception ex) { Log($"Temp script error: {ex.Message}"); }
                    await Task.Delay(10000);
                }
            });
        }
        else if (ts == null) { Log("check-cpu-temp.ps1 NOT FOUND"); }
        else { Log("Not admin, skipping temp collector"); }
    }

    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    public HardwareStats Collect()
    {
        var s = new HardwareStats();

        // Read cache (instant - just reading a file)
        ReadCache();

        // Static data from cache
        s.CpuModel = GetCacheString("cpuName", "CPU");
        s.GpuModel = GetCacheString("gpuName", "GPU");
        s.MotherboardModel = GetCacheString("mbModel", "Motherboard");
        s.DiskName = GetCacheString("diskName", "Disco");
        s.NetworkName = GetCacheString("netName", "Rede");
        s.RamModel = GetCacheString("ramModel", "RAM");
        s.RamSpeed = GetCacheInt("ramSpeed", 0);
        s.RamModuleCount = GetCacheInt("ramModuleCount", 0);
        s.RamModuleSize = GetCacheDouble("ramModuleSize", 0);
        s.RamVoltage = GetCacheDouble("ramVoltage", 0);

        // Dynamic data from cache
        s.CpuUsage = GetCacheDouble("cpuLoad", 0);
        s.GpuUsage = GetCacheDouble("gpuLoad", 0);
        s.TotalRam = GetCacheDouble("ramTotal", 0);
        s.UsedRam = GetCacheDouble("ramUsed", 0);
        s.DiskTotalGb = GetCacheDouble("diskTotal", 0);
        s.DiskUsedGb = GetCacheDouble("diskUsed", 0);
        s.DiskUsagePercent = GetCacheDouble("diskPct", 0);
        s.DiskReadKbps = GetCacheDouble("diskReadKB", 0);
        s.DiskWriteKbps = GetCacheDouble("diskWriteKB", 0);

        // GPU data via nvidia-smi (fallback when cache doesn't have it)
        if (s.GpuUsage == 0 || s.GpuTemp == 0)
        {
            var (gpuLoad, gpuTemp) = ReadGpuViaSmi();
            if (gpuLoad > 0) s.GpuUsage = gpuLoad;
            if (gpuTemp > 0) s.GpuTemp = gpuTemp;
        }

        // Temperatures from LHM PowerShell script (cpu-check-result.txt)
        ReadTempsFromFile(s);
        
        // GPU temp from cache (via nvidia-smi in hw-collector)
        if (s.GpuTemp == 0) s.GpuTemp = GetCacheDouble("gpuTemp", 0);

        // Network speed (calculate from delta)
        var netReceived = GetCacheLong("netReceived", 0);
        var netSent = GetCacheLong("netSent", 0);
        var now = DateTime.UtcNow;
        if (_lastNetworkCheck != DateTime.MinValue && (now - _lastNetworkCheck).TotalSeconds > 0)
        {
            var elapsed = (now - _lastNetworkCheck).TotalSeconds;
            s.NetworkDownloadSpeed = Math.Max(0, (netReceived - _lastBytesReceived) / elapsed / 1024.0);
            s.NetworkUploadSpeed = Math.Max(0, (netSent - _lastBytesSent) / elapsed / 1024.0);
        }
        _lastBytesReceived = netReceived;
        _lastBytesSent = netSent;
        _lastNetworkCheck = now;

        // Fans (cached, update every 5s)
        if ((DateTime.UtcNow - _fansCache).TotalSeconds > 5)
        {
            _cachedFans = ReadFans();
            _fansCache = DateTime.UtcNow;
        }
        s.Fans = _cachedFans;

        // Game detection (cached, update every 3s)
        if ((DateTime.UtcNow - _gameDetectCache).TotalSeconds > 3)
        {
            DetectGame();
            _gameDetectCache = DateTime.UtcNow;
        }
        s.FpsCurrent = _fpsMonitor.Current;
        s.FpsMin = _fpsMonitor.Min;
        s.FpsMax = _fpsMonitor.Max;
        s.FpsAvg = _fpsMonitor.Avg;
        s.FpsSource = _fpsMonitor.Source;

        return s;
    }

    // ===== Cache Reading =====
    
    private void ReadCache()
    {
        if ((DateTime.UtcNow - _lastCacheRead).TotalMilliseconds < 500 && _cacheDoc != null)
            return;

        try
        {
            if (File.Exists(CacheFile))
            {
                var json = File.ReadAllText(CacheFile);
                _cacheDoc?.Dispose();
                _cacheDoc = JsonDocument.Parse(json);
                _lastCacheRead = DateTime.UtcNow;
                var cpu = GetCacheDouble("cpuLoad", -1);
                var ram = GetCacheDouble("ramUsed", -1);
                Log($"Cache OK - cpuLoad:{cpu} ramUsed:{ram}");
            }
        }
        catch (Exception ex) { Log($"Cache error: {ex.Message}"); }
    }

    private string GetCacheString(string key, string fallback)
    {
        try
        {
            if (_cacheDoc != null && _cacheDoc.RootElement.TryGetProperty(key, out var val))
                return val.GetString() ?? fallback;
        }
        catch { }
        return fallback;
    }

    private double GetCacheDouble(string key, double fallback)
    {
        try
        {
            if (_cacheDoc != null && _cacheDoc.RootElement.TryGetProperty(key, out var val))
                return val.GetDouble();
        }
        catch { }
        return fallback;
    }

    private int GetCacheInt(string key, int fallback)
    {
        try
        {
            if (_cacheDoc != null && _cacheDoc.RootElement.TryGetProperty(key, out var val))
                return val.GetInt32();
        }
        catch { }
        return fallback;
    }

    private long GetCacheLong(string key, long fallback)
    {
        try
        {
            if (_cacheDoc != null && _cacheDoc.RootElement.TryGetProperty(key, out var val))
                return val.GetInt64();
        }
        catch { }
        return fallback;
    }

    // ===== Fans =====
    
    private static List<FanInfo> ReadFans()
    {
        var fans = new List<FanInfo>();
        try
        {
            var f = Path.Combine(Path.GetTempPath(), "cpu-check-result.txt");
            if (File.Exists(f))
            {
                foreach (var l in File.ReadAllLines(f))
                {
                    if (l.StartsWith("FAN: "))
                    {
                        var parts = l[5..].Split('=');
                        if (parts.Length == 2 && double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var rpm))
                            fans.Add(new FanInfo { Name = MapFanName(parts[0].Trim()), Rpm = rpm, DutyPercent = 0 });
                    }
                }
            }
        }
        catch { }
        return fans;
    }

    private static string MapFanName(string raw)
    {
        return raw switch
        {
            "Fan #1" => "CHAN_1 (Gabinete)",
            "Fan #2" => "CPU_FAN",
            "Fan #3" => "CHAN_2 (Gabinete)",
            "Fan #4" => "CHAN_3 (Gabinete)",
            "Fan #5" => "OPT_CPU (Bomba WC)",
            "Fan #6" => "OPT_CPU (Bomba WC)",
            "Fan #7" => "OPT_CPU (Bomba WC)",
            "GPU Fan" => "GPU Fan",
            _ => raw
        };
    }

    // ===== Game Detection =====
    
    private void DetectGame()
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo("tasklist", "/FO CSV /NH")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
            if (p == null) return;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);

            string? detected = null;
            foreach (var line in output.Split('\n'))
            {
                var parts = line.Split(',');
                if (parts.Length < 1) continue;
                var name = parts[0].Trim('"', ' ');
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    name = name[..^4];

                if (name.Contains("SB-Win64-Shipping", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("forza", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Minecraft", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Subnautica", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Stellar", StringComparison.OrdinalIgnoreCase))
                {
                    detected = name;
                    break;
                }
            }

            if (detected != null && detected != _lastDetectedGame)
            {
                _lastDetectedGame = detected;
                _fpsMonitor.Start(detected);
            }
            else if (detected == null && _lastDetectedGame != null)
            {
                _lastDetectedGame = null;
                _fpsMonitor.Stop();
            }
        }
        catch { }
    }

    // ===== Temperature Reading =====
    
    private static void ReadTempsFromFile(HardwareStats s)
    {
        try
        {
            var f = Path.Combine(Path.GetTempPath(), "cpu-check-result.txt");
            if (!File.Exists(f)) return;

            foreach (var l in File.ReadAllLines(f))
            {
                if (l.StartsWith("CPU Temp: ") && s.CpuTemp == 0 && double.TryParse(l[10..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var t) && t > 0 && t < 120)
                    s.CpuTemp = Math.Round(t, 1);
                if (l.StartsWith("CPU Package: ") && s.CpuPackageTemp == 0 && double.TryParse(l[13..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var p) && p > 0 && p < 120)
                    s.CpuPackageTemp = Math.Round(p, 1);
                if (l.StartsWith("MB Temp: ") && s.MotherboardTemp == 0 && double.TryParse(l[9..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var mt) && mt > 0 && mt < 120)
                    s.MotherboardTemp = Math.Round(mt, 1);
                if (l.StartsWith("MB VRM: ") && s.MotherboardVrmTemp == 0 && double.TryParse(l[8..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var mv) && mv > 0 && mv < 120)
                    s.MotherboardVrmTemp = Math.Round(mv, 1);
                // Voltages/Power
                if (l.StartsWith("Vcore: ") && s.CpuVoltage == 0 && double.TryParse(l[7..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var vc) && vc > 0 && vc < 5)
                {
                    s.CpuVoltage = Math.Round(vc, 3);
                    s.MotherboardVoltage = Math.Round(vc, 3);
                }
                if (l.StartsWith("GPU Core Voltage: ") && s.GpuVoltage == 0 && double.TryParse(l[18..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var gv) && gv > 0 && gv < 5)
                    s.GpuVoltage = Math.Round(gv, 3);
                if (l.StartsWith("CPU Power: ") && s.CpuPower == 0 && double.TryParse(l[11..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var cp) && cp > 0)
                    s.CpuPower = Math.Round(cp, 1);
                if (l.StartsWith("GPU Power: ") && s.GpuPower == 0 && double.TryParse(l[11..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var gp) && gp > 0)
                    s.GpuPower = Math.Round(gp, 1);
            }
        }
        catch { }
    }

    // ===== GPU via nvidia-smi =====
    
    private static (double load, double temp) ReadGpuViaSmi()
    {
        try
        {
            var psi = new ProcessStartInfo("nvidia-smi")
            {
                Arguments = "--query-gpu=utilization.gpu,temperature.gpu --format=csv,noheader",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                var output = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(3000);
                
                var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+) %, (\d+)");
                if (match.Success)
                {
                    double.TryParse(match.Groups[1].Value, out var load);
                    double.TryParse(match.Groups[2].Value, out var temp);
                    return (load, temp);
                }
            }
        }
        catch { }
        return (0, 0);
    }

    // ===== Utility =====
    
    public void Dispose() { _fpsMonitor.Stop(); }

    public void StartFpsMonitor(string processName) => _fpsMonitor.Start(processName);
    public void StopFpsMonitor() => _fpsMonitor.Stop();

    private static void Log(string msg)
    {
        try
        {
            var f = Path.Combine(Path.GetTempPath(), "eme_hw_monitor.log");
            File.AppendAllText(f, $"{DateTime.Now:HH:mm:ss} - {msg}{Environment.NewLine}");
        }
        catch { }
    }

    private static string? FindToolsFile(string fn)
    {
        var d = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 8; i++) { var p = Path.Combine(d, "tools", fn); if (File.Exists(p)) return p; var pa = Path.GetDirectoryName(d); if (pa == null || pa == d) break; d = pa; }
        return null;
    }
}
