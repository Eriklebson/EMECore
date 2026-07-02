using System.Diagnostics;
using System.Globalization;
using System.Management;
using EMECore.Core.Models;
using LibreHardwareMonitor.Hardware;

namespace EMECore.Hardware.Services;

public class HardwareMonitorService
{
    private readonly Computer _computer;
    private readonly FpsMonitorService _fpsMonitor = new();
    private double _psCachedTemp, _psCachedPkgTemp;
    private DateTime _psCache = DateTime.MinValue;
    private bool _psRunning;
    private double _cpuPackageTemp;
    private string? _lastDetectedGame;
    
    private string? _cachedCpuModel;
    private string? _cachedGpuModel;
    private int _cachedRamSpeed;
    private List<FanInfo> _cachedFans = new();
    private DateTime _fansCache = DateTime.MinValue;
    private DateTime _gameDetectCache = DateTime.MinValue;

    public HardwareMonitorService()
    {
        _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true, IsMotherboardEnabled = true, IsMemoryEnabled = true };
        try { _computer.Open(); foreach (var h in _computer.Hardware) { h.Update(); foreach (var z in h.SubHardware) z.Update(); } }
        catch { }
    }

    public HardwareStats Collect()
    {
        var s = new HardwareStats();
        try { foreach (var h in _computer.Hardware) { h.Update(); foreach (var z in h.SubHardware) z.Update(); } } catch { }

        s.CpuUsage = ReadCpuUsage();
        s.CpuModel = _cachedCpuModel ??= ReadCpuModel();
        s.CpuTemp = ReadCpuTemp();
        s.CpuPackageTemp = _cpuPackageTemp;

        s.GpuUsage = ReadGpuUsage();
        s.GpuModel = _cachedGpuModel ??= ReadGpuModel();
        s.GpuTemp = ReadGpuTemp();
        s.GpuHotspotTemp = ReadGpuHotspot();

        s.TotalRam = ReadTotalRamGb();
        s.UsedRam = ReadUsedRamGb();
        s.RamSpeed = _cachedRamSpeed != 0 ? _cachedRamSpeed : (_cachedRamSpeed = ReadRamSpeed());
        
        if ((DateTime.UtcNow - _fansCache).TotalSeconds > 5)
        {
            _cachedFans = ReadFans();
            _fansCache = DateTime.UtcNow;
        }
        s.Fans = _cachedFans;

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

    private double ReadCpuUsage()
    {
        foreach (var h in _computer.Hardware)
        {
            if (h.HardwareType != HardwareType.Cpu) continue;
            foreach (var sn in h.Sensors)
                if (sn.SensorType == SensorType.Load && sn.Value.HasValue && sn.Name.Contains("Total"))
                    return Math.Round(sn.Value.Value, 1);
            foreach (var sn in h.Sensors)
                if (sn.SensorType == SensorType.Load && sn.Value.HasValue)
                    return Math.Round(sn.Value.Value, 1);
        }
        try { using var sr = new ManagementObjectSearcher("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'"); foreach (var o in sr.Get()) return Math.Round(Convert.ToDouble(o["PercentProcessorTime"]), 1); } catch { }
        return 0;
    }

    private double ReadCpuTemp()
    {
        foreach (var h in _computer.Hardware)
        {
            if (h.HardwareType != HardwareType.Cpu) continue;
            foreach (var sn in h.Sensors)
                if (sn.SensorType == SensorType.Temperature && sn.Value.HasValue && sn.Value > 0)
                    return Math.Round(sn.Value.Value, 1);
            foreach (var sub in h.SubHardware)
                foreach (var sn in sub.Sensors)
                    if (sn.SensorType == SensorType.Temperature && sn.Value.HasValue && sn.Value > 0)
                        return Math.Round(sn.Value.Value, 1);
        }
        return ReadCpuTempPs();
    }

    private double ReadGpuUsage()
    {
        var v = NvidiaSmi("utilization.gpu"); if (v > 0) return v;
        foreach (var h in _computer.Hardware)
            if (h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd)
                foreach (var sn in h.Sensors)
                    if (sn.SensorType == SensorType.Load && sn.Value.HasValue && sn.Value > 0) return Math.Round(sn.Value.Value, 1);
        return 0;
    }

    private double ReadGpuTemp()
    {
        var v = NvidiaSmi("temperature.gpu"); if (v > 0) return v;
        foreach (var h in _computer.Hardware)
            if (h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd)
                foreach (var sn in h.Sensors)
                    if (sn.SensorType == SensorType.Temperature && sn.Value.HasValue && sn.Value > 0 && !sn.Name.Contains("Hot"))
                        return Math.Round(sn.Value.Value, 1);
        return 0;
    }

    private double ReadGpuHotspot()
    {
        foreach (var h in _computer.Hardware)
            if (h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd)
                foreach (var sn in h.Sensors)
                    if (sn.SensorType == SensorType.Temperature && sn.Value.HasValue && sn.Value > 0 && sn.Name.Contains("Hot"))
                        return Math.Round(sn.Value.Value, 1);
        return 0;
    }

    private static double NvidiaSmi(string q)
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo("nvidia-smi", $"--query-gpu={q} --format=csv,noheader,nounits")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
            if (p == null) return 0;
            var o = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(3000);
            return double.TryParse(o, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? Math.Round(v, 1) : 0;
        }
        catch { return 0; }
    }

    private double ReadCpuTempPs()
    {
        try
        {
            if ((DateTime.UtcNow - _psCache).TotalSeconds < 2 && _psCachedTemp > 0)
            {
                _cpuPackageTemp = _psCachedPkgTemp > 0 ? _psCachedPkgTemp : _psCachedTemp;
                return _psCachedTemp;
            }

            if (!_psRunning)
            {
                _psRunning = true;
                var sp = FindToolsFile("check-cpu-temp.ps1");
                if (sp != null)
                    Task.Run(() => { try { Process.Start(new ProcessStartInfo("powershell.exe", $"-ExecutionPolicy Bypass -File \"{sp}\"") { UseShellExecute = true, Verb = "runas", CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden })?.WaitForExit(15000); } catch { } _psRunning = false; });
                else _psRunning = false;
            }

            var f = Path.Combine(Path.GetTempPath(), "cpu-check-result.txt");
            if (File.Exists(f))
            {
                double? cpu = null, pkg = null;
                foreach (var l in File.ReadAllLines(f))
                {
                    if (l.StartsWith("CPU Temp: ") && cpu == null && double.TryParse(l[10..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var tv) && tv > 0 && tv < 120) cpu = Math.Round(tv, 1);
                    if (l.StartsWith("CPU Package: ") && pkg == null && double.TryParse(l[13..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var pv) && pv > 0 && pv < 120) pkg = Math.Round(pv, 1);
                }
                if (cpu.HasValue) { _psCachedTemp = cpu.Value; _psCachedPkgTemp = pkg ?? cpu.Value; _psCache = DateTime.UtcNow; }
            }
        }
        catch { }
        _cpuPackageTemp = _psCachedPkgTemp > 0 ? _psCachedPkgTemp : _psCachedTemp;
        return _psCachedTemp;
    }

    private static string ReadCpuModel()
    {
        try { using var s = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"); foreach (var o in s.Get()) return o["Name"]?.ToString()?.Trim() ?? "CPU"; } catch { }
        return "CPU";
    }

    private static string ReadGpuModel()
    {
        try { using var s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"); foreach (var o in s.Get()) return o["Name"]?.ToString()?.Trim() ?? "GPU"; } catch { }
        return "GPU";
    }

    private static double ReadTotalRamGb()
    {
        try { using var s = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"); foreach (var o in s.Get()) return Math.Round(Convert.ToDouble(o["TotalVisibleMemorySize"]) / 1048576.0, 1); } catch { }
        return 0;
    }

    private static double ReadUsedRamGb()
    {
        try { using var s = new ManagementObjectSearcher("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem"); foreach (var o in s.Get()) return Math.Round(Convert.ToDouble(o["TotalVisibleMemorySize"]) / 1048576.0 - Convert.ToDouble(o["FreePhysicalMemory"]) / 1048576.0, 1); } catch { }
        return 0;
    }

    private static int ReadRamSpeed()
    {
        try { using var s = new ManagementObjectSearcher("SELECT ConfiguredClockSpeed FROM Win32_PhysicalMemory"); foreach (var o in s.Get()) if (o["ConfiguredClockSpeed"] != null) return Convert.ToInt32(o["ConfiguredClockSpeed"]); } catch { }
        return 0;
    }

    private static List<FanInfo> ReadFans()
    {
        var fans = new List<FanInfo>();
        try
        {
            var f = Path.Combine(Path.GetTempPath(), "cpu-check-result.txt");
            if (!File.Exists(f)) return fans;

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

    private static string? FindToolsFile(string fn)
    {
        var d = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 8; i++) { var p = Path.Combine(d, "tools", fn); if (File.Exists(p)) return p; var pa = Path.GetDirectoryName(d); if (pa == null || pa == d) break; d = pa; }
        return null;
    }

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

    public void Dispose() { try { _computer.Close(); } catch { } _fpsMonitor.Stop(); }

    public void StartFpsMonitor(string processName) => _fpsMonitor.Start(processName);
    public void StopFpsMonitor() => _fpsMonitor.Stop();
}
