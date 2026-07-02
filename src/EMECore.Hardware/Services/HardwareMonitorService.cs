using System.Diagnostics;
using System.Globalization;
using System.Management;
using EMECore.Core.Models;
using LibreHardwareMonitor.Hardware;

namespace EMECore.Hardware.Services;

public class HardwareMonitorService
{
    private readonly Computer _computer;
    private double _psCpu, _psPkg;
    private DateTime _psCache = DateTime.MinValue;
    private bool _psRunning;
    private double _cpuPackageTemp;

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
        s.CpuModel = ReadCpuModel();
        s.CpuTemp = ReadCpuTemp();
        s.CpuPackageTemp = _cpuPackageTemp;

        s.GpuUsage = ReadGpuUsage();
        s.GpuModel = ReadGpuModel();
        s.GpuTemp = ReadGpuTemp();

        s.TotalRam = ReadTotalRamGb();
        s.UsedRam = ReadUsedRamGb();
        s.RamSpeed = ReadRamSpeed();
        s.Fans = ReadFans();
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
                    if (sn.SensorType == SensorType.Temperature && sn.Value.HasValue && sn.Value > 0) return Math.Round(sn.Value.Value, 1);
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
            if ((DateTime.UtcNow - _psCache).TotalSeconds < 2 && _psCpu > 0)
            {
                _cpuPackageTemp = _psPkg > 0 ? _psPkg : _psCpu;
                return _psCpu;
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
                if (cpu.HasValue) { _psCpu = cpu.Value; _psPkg = pkg ?? cpu.Value; _psCache = DateTime.UtcNow; }
            }
        }
        catch { }
        _cpuPackageTemp = _psPkg > 0 ? _psPkg : _psCpu;
        return _psCpu;
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

    private static List<FanInfo> ReadFans() => new();

    private static string? FindToolsFile(string fn)
    {
        var d = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 8; i++) { var p = Path.Combine(d, "tools", fn); if (File.Exists(p)) return p; var pa = Path.GetDirectoryName(d); if (pa == null || pa == d) break; d = pa; }
        return null;
    }

    public void Dispose() { try { _computer.Close(); } catch { } }
}
