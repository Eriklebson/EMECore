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
    private string? _cachedMotherboardModel;
    private string? _cachedRamModel;
    private int _cachedRamModuleCount;
    private double _cachedRamModuleSize;
    private int _cachedRamSpeed;
    private List<FanInfo> _cachedFans = new();
    private DateTime _fansCache = DateTime.MinValue;
    private DateTime _gameDetectCache = DateTime.MinValue;

    // Network speed tracking
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastNetworkCheck = DateTime.MinValue;

    public HardwareMonitorService()
    {
        _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true, IsMotherboardEnabled = true, IsMemoryEnabled = true };
        try
        {
            _computer.Open();
            foreach (var h in _computer.Hardware) { h.Update(); foreach (var z in h.SubHardware) z.Update(); }
            Log("LibreHardwareMonitor initialized successfully");
        }
        catch (Exception ex)
        {
            Log($"LibreHardwareMonitor init error: {ex.Message}");
        }
    }

    public HardwareStats Collect()
    {
        var s = new HardwareStats();
        try { foreach (var h in _computer.Hardware) { h.Update(); foreach (var z in h.SubHardware) z.Update(); } } catch { }

        // Run PowerShell script periodically for temps and fans
        if (!_psRunning)
        {
            _psRunning = true;
            var sp = FindToolsFile("check-cpu-temp.ps1");
            if (sp != null)
            {
                Task.Run(() =>
                {
                    try
                    {
                        var psi = new ProcessStartInfo("powershell.exe")
                        {
                            Arguments = $"-ExecutionPolicy Bypass -File \"{sp}\"",
                            UseShellExecute = true,
                            Verb = "runas",
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        };
                        var p = Process.Start(psi);
                        p?.WaitForExit(15000);
                    }
                    catch { }
                    _psRunning = false;
                });
            }
            else _psRunning = false;
        }

        s.MotherboardModel = _cachedMotherboardModel ??= ReadMotherboardModel();
        s.MotherboardTemp = ReadMotherboardTemp();
        s.MotherboardVrmTemp = ReadMotherboardVrmTemp();

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
        s.RamModel = _cachedRamModel ??= ReadRamModel();
        s.RamModuleCount = _cachedRamModuleCount != 0 ? _cachedRamModuleCount : (_cachedRamModuleCount = ReadRamModuleCount());
        s.RamModuleSize = _cachedRamModuleSize != 0 ? _cachedRamModuleSize : (_cachedRamModuleSize = ReadRamModuleSize());

        // Disk
        s.DiskName = ReadDiskName();
        var diskInfo = ReadDiskInfo();
        s.DiskTotalGb = diskInfo.total;
        s.DiskUsedGb = diskInfo.used;
        s.DiskUsagePercent = diskInfo.percent;
        var diskSpeed = ReadDiskSpeed();
        s.DiskReadKbps = diskSpeed.readKbps;
        s.DiskWriteKbps = diskSpeed.writeKbps;

        // Network
        s.NetworkName = ReadNetworkName();
        var netSpeed = ReadNetworkSpeed();
        s.NetworkDownloadSpeed = netSpeed.download;
        s.NetworkUploadSpeed = netSpeed.upload;
        
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
        // PowerShell fallback
        var result = RunPowerShell("(Get-CimInstance Win32_Processor | Select-Object -First 1).LoadPercentage", "0");
        return double.TryParse(result, out var val) ? val : 0;
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
        // PowerShell fallback
        var result = RunPowerShell("(Get-CimInstance Win32_VideoController | Select-Object -First 1).LoadPercentage", "0");
        return double.TryParse(result, out var val) ? val : 0;
    }

    private double ReadGpuTemp()
    {
        var v = NvidiaSmi("temperature.gpu"); if (v > 0) return v;
        foreach (var h in _computer.Hardware)
            if (h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd)
                foreach (var sn in h.Sensors)
                    if (sn.SensorType == SensorType.Temperature && sn.Value.HasValue && sn.Value > 0 && !sn.Name.Contains("Hot"))
                        return Math.Round(sn.Value.Value, 1);
        // PowerShell fallback
        var result = RunPowerShell("Get-CimInstance Win32_TemperatureSensor | Select-Object -First 1 | ForEach-Object { [math]::Round($_.CurrentTemperature, 1) }", "0");
        return double.TryParse(result, out var val) ? val : 0;
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
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var o in s.Get()) return o["Name"]?.ToString()?.Trim() ?? "CPU";
        }
        catch (Exception ex) { Log($"ReadCpuModel WMI error: {ex.Message}"); }
        // Fallback: PowerShell
        return RunPowerShell("Get-CimInstance Win32_Processor | Select-Object -ExpandProperty Name", "CPU");
    }

    private static string ReadGpuModel()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (var o in s.Get()) return o["Name"]?.ToString()?.Trim() ?? "GPU";
        }
        catch (Exception ex) { Log($"ReadGpuModel WMI error: {ex.Message}"); }
        return RunPowerShell("Get-CimInstance Win32_VideoController | Select-Object -ExpandProperty Name", "GPU");
    }

    private static string ReadMotherboardModel()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Product FROM Win32_BaseBoard");
            foreach (var o in s.Get()) return o["Product"]?.ToString()?.Trim() ?? "Motherboard";
        }
        catch (Exception ex) { Log($"ReadMotherboardModel WMI error: {ex.Message}"); }
        return RunPowerShell("Get-CimInstance Win32_BaseBoard | Select-Object -ExpandProperty Product", "Motherboard");
    }

    private double ReadMotherboardTemp()
    {
        // Try direct LHM reading first
        foreach (var h in _computer.Hardware)
        {
            if (h.HardwareType != HardwareType.Motherboard) continue;
            foreach (var sn in h.Sensors)
                if (sn.SensorType == SensorType.Temperature && sn.Value.HasValue && sn.Value > 0 && sn.Value < 120)
                    return Math.Round(sn.Value.Value, 1);
            foreach (var sub in h.SubHardware)
                foreach (var sn in sub.Sensors)
                    if (sn.SensorType == SensorType.Temperature && sn.Value.HasValue && sn.Value > 0 && sn.Value < 120)
                        return Math.Round(sn.Value.Value, 1);
        }
        // Fallback: read from PowerShell output file
        return ReadMotherboardTempFromFile("MB Temp");
    }

    private double ReadMotherboardVrmTemp()
    {
        // Try direct LHM reading first
        foreach (var h in _computer.Hardware)
        {
            if (h.HardwareType != HardwareType.Motherboard) continue;
            foreach (var sn in h.Sensors)
                if (sn.SensorType == SensorType.Temperature && sn.Value.HasValue && sn.Value > 0 && sn.Value < 120 &&
                    (sn.Name.Contains("VRM", StringComparison.OrdinalIgnoreCase) ||
                     sn.Name.Contains("Voltage", StringComparison.OrdinalIgnoreCase) ||
                     sn.Name.Contains("PWM", StringComparison.OrdinalIgnoreCase)))
                    return Math.Round(sn.Value.Value, 1);
            foreach (var sub in h.SubHardware)
                foreach (var sn in sub.Sensors)
                    if (sn.SensorType == SensorType.Temperature && sn.Value.HasValue && sn.Value > 0 && sn.Value < 120 &&
                        (sn.Name.Contains("VRM", StringComparison.OrdinalIgnoreCase) ||
                         sn.Name.Contains("Voltage", StringComparison.OrdinalIgnoreCase) ||
                         sn.Name.Contains("PWM", StringComparison.OrdinalIgnoreCase)))
                        return Math.Round(sn.Value.Value, 1);
        }
        // Fallback: read from PowerShell output file
        return ReadMotherboardTempFromFile("MB VRM");
    }

    private static double ReadMotherboardTempFromFile(string prefix)
    {
        try
        {
            var f = Path.Combine(Path.GetTempPath(), "cpu-check-result.txt");
            if (!File.Exists(f)) return 0;
            foreach (var l in File.ReadAllLines(f))
            {
                if (l.StartsWith($"{prefix}: ") && double.TryParse(l[(prefix.Length + 2)..].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tv) && tv > 0 && tv < 120)
                    return Math.Round(tv, 1);
            }
        }
        catch { }
        return 0;
    }

    private static double ReadTotalRamGb()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
            foreach (var o in s.Get()) return Math.Round(Convert.ToDouble(o["TotalVisibleMemorySize"]) / 1048576.0, 1);
        }
        catch { }
        var result = RunPowerShell("Get-CimInstance Win32_OperatingSystem | Select-Object -ExpandProperty TotalVisibleMemorySize", "0");
        return double.TryParse(result, out var val) ? Math.Round(val / 1048576.0, 1) : 0;
    }

    private static double ReadUsedRamGb()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
            foreach (var o in s.Get()) return Math.Round(Convert.ToDouble(o["TotalVisibleMemorySize"]) / 1048576.0 - Convert.ToDouble(o["FreePhysicalMemory"]) / 1048576.0, 1);
        }
        catch { }
        // PowerShell fallback
        var totalResult = RunPowerShell("(Get-CimInstance Win32_OperatingSystem).TotalVisibleMemorySize", "0");
        var freeResult = RunPowerShell("(Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory", "0");
        if (double.TryParse(totalResult, out var total) && double.TryParse(freeResult, out var free) && total > 0)
            return Math.Round(total / 1048576.0 - free / 1048576.0, 1);
        return 0;
    }

    private static int ReadRamSpeed()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT ConfiguredClockSpeed FROM Win32_PhysicalMemory");
            foreach (var o in s.Get()) if (o["ConfiguredClockSpeed"] != null) return Convert.ToInt32(o["ConfiguredClockSpeed"]);
        }
        catch { }
        var result = RunPowerShell("Get-CimInstance Win32_PhysicalMemory | Select-Object -First 1 -ExpandProperty ConfiguredClockSpeed", "0");
        return int.TryParse(result, out var val) ? val : 0;
    }

    private static string ReadRamModel()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Manufacturer, PartNumber FROM Win32_PhysicalMemory");
            foreach (var o in s.Get())
            {
                var mfg = o["Manufacturer"]?.ToString()?.Trim() ?? "";
                var part = o["PartNumber"]?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(mfg) && !string.IsNullOrEmpty(part)) return $"{mfg} {part}";
                if (!string.IsNullOrEmpty(part)) return part;
                if (!string.IsNullOrEmpty(mfg)) return mfg;
            }
        }
        catch { }
        // PowerShell fallback - using simpler command
        return RunPowerShell("(Get-CimInstance Win32_PhysicalMemory | Select-Object -First 1).Manufacturer + ' ' + (Get-CimInstance Win32_PhysicalMemory | Select-Object -First 1).PartNumber", "RAM");
    }

    private static int ReadRamModuleCount()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            return s.Get().Count;
        }
        catch { }
        var result = RunPowerShell("(Get-CimInstance Win32_PhysicalMemory).Count", "0");
        return int.TryParse(result, out var val) ? val : 0;
    }

    private static double ReadRamModuleSize()
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            foreach (var o in s.Get())
                return Math.Round(Convert.ToDouble(o["Capacity"]) / 1073741824.0, 0);
        }
        catch { }
        var result = RunPowerShell("Get-CimInstance Win32_PhysicalMemory | Select-Object -First 1 -ExpandProperty Capacity", "0");
        return double.TryParse(result, out var val) ? Math.Round(val / 1073741824.0, 0) : 0;
    }

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

            // If no fans found from file, try PowerShell
            if (fans.Count == 0)
            {
                fans = ReadFansViaPowerShell();
            }
        }
        catch { }
        return fans;
    }

    private static List<FanInfo> ReadFansViaPowerShell()
    {
        var fans = new List<FanInfo>();
        try
        {
            // Try to get fan info from WMI (works on some systems)
            var result = RunPowerShell("Get-CimInstance Win32_Fan | Select-Object Name, DesiredSpeed | ForEach-Object { Write-Output ($_.Name + '=' + $_.DesiredSpeed) }", "");
            if (!string.IsNullOrEmpty(result))
            {
                foreach (var line in result.Split('\n'))
                {
                    var parts = line.Trim().Split('=');
                    if (parts.Length == 2 && double.TryParse(parts[1].Trim(), out var rpm) && rpm > 0)
                        fans.Add(new FanInfo { Name = MapFanName(parts[0].Trim()), Rpm = rpm });
                }
            }

            // If still no fans, try thermal zone
            if (fans.Count == 0)
            {
                var thermalResult = RunPowerShell("Get-CimInstance MSAcpi_ThermalZoneTemperature -Namespace 'root/wmi' | Select-Object -First 1 | ForEach-Object { [math]::Round(($_.CurrentTemperature - 2732) / 10.0, 1) }", "0");
                if (double.TryParse(thermalResult, out var temp) && temp > 0)
                {
                    fans.Add(new FanInfo { Name = "Thermal Zone", Rpm = 0 });
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

    // ===== DISK =====
    private static string ReadDiskName()
    {
        try
        {
            var result = RunPowerShell("Get-PhysicalDisk | Select-Object -First 1 -ExpandProperty Model", "Disco");
            return result;
        }
        catch { return "Disco"; }
    }

    private static (double total, double used, double percent) ReadDiskInfo()
    {
        try
        {
            var result = RunPowerShell("Get-CimInstance Win32_LogicalDisk | Where-Object { $_.DeviceID -eq 'C:' } | Select-Object Size, FreeSpace | ForEach-Object { Write-Output ($_.Size.ToString() + '|' + $_.FreeSpace.ToString()) }", "");
            if (!string.IsNullOrEmpty(result))
            {
                var parts = result.Split('|');
                if (parts.Length == 2 && double.TryParse(parts[0], out var total) && double.TryParse(parts[1], out var free))
                {
                    var totalGb = Math.Round(total / 1073741824.0, 1);
                    var freeGb = Math.Round(free / 1073741824.0, 1);
                    var usedGb = Math.Round(totalGb - freeGb, 1);
                    var pct = totalGb > 0 ? Math.Round(usedGb / totalGb * 100, 1) : 0;
                    return (totalGb, usedGb, pct);
                }
            }
        }
        catch { }
        return (0, 0, 0);
    }

    private static (double readKbps, double writeKbps) ReadDiskSpeed()
    {
        try
        {
            var result = RunPowerShell("Get-CimInstance Win32_PerfFormattedData_PerfDisk_PhysicalDisk | Where-Object { $_.Name -eq '_Total' } | Select-Object DiskReadBytesPersec, DiskWriteBytesPersec | ForEach-Object { Write-Output ($_.DiskReadBytesPersec.ToString() + '|' + $_.DiskWriteBytesPersec.ToString()) }", "");
            if (!string.IsNullOrEmpty(result))
            {
                var parts = result.Split('|');
                if (parts.Length == 2 && double.TryParse(parts[0], out var read) && double.TryParse(parts[1], out var write))
                {
                    return (Math.Round(read / 1024.0, 1), Math.Round(write / 1024.0, 1));
                }
            }
        }
        catch { }
        return (0, 0);
    }

    // ===== NETWORK =====
    private static string ReadNetworkName()
    {
        try
        {
            var result = RunPowerShell("Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.InterfaceDescription -notmatch 'Loopback|Tunnel|VPN' } | Select-Object -First 1 -ExpandProperty InterfaceDescription", "Rede");
            return result;
        }
        catch { return "Rede"; }
    }

    private (double download, double upload) ReadNetworkSpeed()
    {
        try
        {
            var now = DateTime.UtcNow;
            if ((now - _lastNetworkCheck).TotalMilliseconds < 500 && _lastNetworkCheck != DateTime.MinValue)
                return (0, 0);

            var result = RunPowerShell("Get-NetAdapterStatistics | Where-Object { $_.Name -notmatch 'Loopback|Tunnel|VPN' } | Select-Object -First 1 | ForEach-Object { Write-Output ($_.ReceivedBytes.ToString() + '|' + $_.SentBytes.ToString()) }", "");
            if (!string.IsNullOrEmpty(result))
            {
                var parts = result.Split('|');
                if (parts.Length == 2 && long.TryParse(parts[0], out var received) && long.TryParse(parts[1], out var sent))
                {
                    var elapsed = (now - _lastNetworkCheck).TotalSeconds;
                    if (_lastNetworkCheck != DateTime.MinValue && elapsed > 0)
                    {
                        var downloadSpeed = (received - _lastBytesReceived) / elapsed / 1024.0; // KB/s
                        var uploadSpeed = (sent - _lastBytesSent) / elapsed / 1024.0; // KB/s
                        _lastBytesReceived = received;
                        _lastBytesSent = sent;
                        _lastNetworkCheck = now;
                        return (Math.Max(0, downloadSpeed), Math.Max(0, uploadSpeed));
                    }
                    _lastBytesReceived = received;
                    _lastBytesSent = sent;
                    _lastNetworkCheck = now;
                }
            }
        }
        catch { }
        return (0, 0);
    }

    private static void Log(string msg)
    {
        try
        {
            var f = Path.Combine(Path.GetTempPath(), "eme_hw_monitor.log");
            File.AppendAllText(f, $"{DateTime.Now:HH:mm:ss} - {msg}{Environment.NewLine}");
        }
        catch { }
    }

    private static string RunPowerShell(string command, string fallback)
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe")
            {
                Arguments = $"-NoProfile -NonInteractive -Command \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                var output = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(5000);
                if (!string.IsNullOrEmpty(output) && !output.Contains("CategoryInfo"))
                    return output;
            }
        }
        catch { }
        return fallback;
    }
}
