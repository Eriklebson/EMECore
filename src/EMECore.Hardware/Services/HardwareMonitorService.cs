using System.Diagnostics;
using System.Management;
using EMECore.Core.Models;
using LibreHardwareMonitor.Hardware;

namespace EMECore.Hardware.Services;

public class HardwareMonitorService
{
    private readonly FpsMonitorService _fpsMonitor = new();
    private readonly LhmHardwareService _lhm = new();
    private readonly MappingService _mapping = MappingService.Instance;
    private readonly CpuSensorMappingService _cpuMapping = CpuSensorMappingService.Instance;
    public readonly GamepadService GamepadService = new();
    private string? _lastDetectedGame;
    private DateTime _gameDetectCache = DateTime.MinValue;
    private List<FanInfo> _cachedFans = new();
    private DateTime _fansCache = DateTime.MinValue;
    private bool _motherboardDetected;
    private bool _cpuDetected;

    // Network delta tracking
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastNetworkCheck = DateTime.MinValue;

    // LHM network delta tracking
    private double _lastLhmBytesReceived;
    private double _lastLhmBytesSent;
    private DateTime _lastLhmNetworkCheck = DateTime.MinValue;
    private string _cachedNetworkName = "";

    public HardwareMonitorService()
    {
        _mapping.Load();
        _cpuMapping.Load();
        _lhm.Update();
    }

    public HardwareStats Collect()
    {
        return CollectInternal(includeWmi: true, refreshLhm: false);
    }

    public HardwareStats CollectFast()
    {
        return CollectInternal(includeWmi: false, refreshLhm: true);
    }

    private HardwareStats? _lastLhmStats;
    private List<DiskInfo> _cachedDisks = new();

    private HardwareStats CollectInternal(bool includeWmi, bool refreshLhm)
    {
        var s = new HardwareStats();

        if (refreshLhm)
        {
            CollectFromLhm(s);
            _lastLhmStats = s;
        }
        else if (_lastLhmStats != null)
        {
            CopyLhmData(_lastLhmStats, s);
        }

        // Always use cached disks list
        s.Disks = _cachedDisks;

        if (includeWmi)
        {
            // WMI (~1-3s) — slow, only when requested
            CollectRamData(s);
            CollectDiskData(s);
            _cachedDisks = s.Disks;

            // Get adapter name via WMI (only once or on first WMI call)
            if (string.IsNullOrEmpty(_cachedNetworkName))
            {
                var (netName, _, _) = CollectNetworkData();
                _cachedNetworkName = netName;
            }

            if (s.GpuUsage == 0 || s.GpuTemp == 0)
            {
                var (gpuLoad, gpuTemp) = ReadGpuViaSmi();
                if (gpuLoad > 0) s.GpuUsage = gpuLoad;
                if (gpuTemp > 0) s.GpuTemp = gpuTemp;
            }

            s.Monitors = CollectMonitorData();
        }

        // Network via LHM (fast, always runs — ~0.1ms)
        CollectNetworkFromLhm(s);

        // Fans (cached, fast)
        if ((DateTime.UtcNow - _fansCache).TotalSeconds > 5)
        {
            var lhmFans = _cachedFans.Where(f => f.Rpm > 0).ToList();
            _cachedFans = lhmFans;
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

        s.Gamepads = CollectGamepadData();

        // Always collect disk speeds (lightweight perf counter query)
        CollectDiskSpeeds(s);

        return s;
    }

    private static void CollectDiskSpeeds(HardwareStats s)
    {
        if (s.Disks.Count == 0) return;
        try
        {
            using var perfSearcher = new ManagementObjectSearcher(
                "SELECT Name, DiskReadBytesPersec, DiskWriteBytesPersec FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk").Get();
            foreach (ManagementObject perf in perfSearcher)
            {
                var name = perf["Name"]?.ToString() ?? "";
                if (name == "_Total" || !name.Contains(" ")) continue;

                var readBytes = Convert.ToDouble(perf["DiskReadBytesPersec"] ?? 0);
                var writeBytes = Convert.ToDouble(perf["DiskWriteBytesPersec"] ?? 0);

                foreach (var ld in s.Disks)
                {
                    var letter = ld.DeviceId.Replace(":", "");
                    if (name.Contains(letter))
                    {
                        ld.ReadKbps = Math.Round(readBytes / 1024, 1);
                        ld.WriteKbps = Math.Round(writeBytes / 1024, 1);
                    }
                }
            }
        }
        catch { }
    }

    private static void CopyLhmData(HardwareStats src, HardwareStats dst)
    {
        dst.CpuModel = src.CpuModel;
        dst.CpuUsage = src.CpuUsage;
        dst.CpuTemp = src.CpuTemp;
        dst.CpuPackageTemp = src.CpuPackageTemp;
        dst.CpuVoltage = src.CpuVoltage;
        dst.CpuPower = src.CpuPower;
        dst.CpuCores = src.CpuCores;
        dst.GpuModel = src.GpuModel;
        dst.GpuUsage = src.GpuUsage;
        dst.GpuTemp = src.GpuTemp;
        dst.GpuHotspotTemp = src.GpuHotspotTemp;
        dst.GpuVoltage = src.GpuVoltage;
        dst.GpuPower = src.GpuPower;
        dst.MotherboardModel = src.MotherboardModel;
        dst.MotherboardTemp = src.MotherboardTemp;
        dst.MotherboardVrmTemp = src.MotherboardVrmTemp;
        dst.MotherboardVoltage = src.MotherboardVoltage;
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
        catch (Exception ex)
        {
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "eme_detect.log"),
                $"{DateTime.Now:HH:mm:ss} ERROR: {ex.Message}\n");
        }
    }

    // ===== WMI Data Collection (RAM, Disk, Network) =====
    
    private static void CollectRamData(HardwareStats s)
    {
        try
        {
            using var os = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem").Get().Cast<ManagementObject>().FirstOrDefault();
            if (os != null)
            {
                s.TotalRam = Math.Round(Convert.ToDouble(os["TotalVisibleMemorySize"]) * 1024 / 1073741824, 1);
                s.UsedRam = Math.Round(s.TotalRam - Convert.ToDouble(os["FreePhysicalMemory"]) * 1024 / 1073741824, 1);
            }

            using var mem = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory").Get();
            var modules = mem.Cast<ManagementObject>().ToList();
            if (modules.Count > 0)
            {
                var first = modules[0];
                s.RamModel = $"{first["Manufacturer"]} {first["PartNumber"]}".Trim();
                s.RamSpeed = Convert.ToInt32(first["ConfiguredClockSpeed"]);
                s.RamModuleCount = modules.Count;
                s.RamModuleSize = Math.Round(Convert.ToDouble(first["Capacity"]) / 1073741824, 0);
                if (first["ConfiguredVoltage"] != null)
                    s.RamVoltage = Math.Round(Convert.ToDouble(first["ConfiguredVoltage"]) / 1000.0, 1);
            }
        }
        catch { }
    }

    private static void CollectDiskData(HardwareStats s)
    {
        try
        {
            // Query ALL logical disks
            using var searcher = new ManagementObjectSearcher("SELECT DeviceID, VolumeName, FileSystem, DriveType, Size, FreeSpace FROM Win32_LogicalDisk").Get();
            foreach (ManagementObject disk in searcher)
            {
                var driveType = Convert.ToInt32(disk["DriveType"]);
                if (driveType != 3 && driveType != 2) continue;

                var deviceId = disk["DeviceID"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(deviceId)) continue;

                var sizeBytes = Convert.ToDouble(disk["Size"] ?? 0);
                var freeBytes = Convert.ToDouble(disk["FreeSpace"] ?? 0);
                if (sizeBytes <= 0) continue;

                var totalGb = Math.Round(sizeBytes / 1073741824, 1);
                var freeGb = Math.Round(freeBytes / 1073741824, 1);
                var usedGb = Math.Round(totalGb - freeGb, 1);
                var usagePct = Math.Round(usedGb / totalGb * 100, 0);

                var volumeName = disk["VolumeName"]?.ToString() ?? "";
                var model = !string.IsNullOrEmpty(volumeName) ? volumeName : deviceId;

                s.Disks.Add(new DiskInfo
                {
                    DeviceId = deviceId,
                    Model = model,
                    VolumeName = volumeName,
                    FileSystem = disk["FileSystem"]?.ToString() ?? "",
                    DriveType = driveType,
                    TotalGb = totalGb,
                    UsedGb = usedGb,
                    FreeGb = freeGb,
                    UsagePercent = usagePct
                });
            }

            // Physical disk models (separate query, cached)
            try
            {
                using var physDisks = new ManagementObjectSearcher("SELECT DeviceID, Model FROM Win32_PhysicalDisk").Get();
                foreach (ManagementObject pd in physDisks)
                {
                    var devId = pd["DeviceID"]?.ToString() ?? "";
                    var model = pd["Model"]?.ToString() ?? "";
                    // Match physical disk index to logical disk via Win32_LogicalDiskToPartition
                    foreach (var ld in s.Disks)
                    {
                        var letter = ld.DeviceId.Replace(":", "");
                        if (devId.Contains(ld.DeviceId.Replace(":", "")) || devId == letter)
                        {
                            if (!string.IsNullOrEmpty(model))
                                ld.Model = model;
                        }
                    }
                }
            }
            catch { }

            // Fallback: if model mapping failed, try VolumeName or "Disco" for each disk
            foreach (var ld in s.Disks)
            {
                if (string.IsNullOrEmpty(ld.Model) || ld.Model == ld.DeviceId)
                {
                    ld.Model = !string.IsNullOrEmpty(ld.VolumeName) ? ld.VolumeName : $"Disco {ld.DeviceId}";
                }
            }

            // Fallback for old single-disk fields (backward compat)
            if (s.Disks.Count > 0)
            {
                var first = s.Disks.FirstOrDefault(d => d.DeviceId == "C:") ?? s.Disks[0];
                s.DiskName = first.Model;
                s.DiskTotalGb = first.TotalGb;
                s.DiskUsedGb = first.UsedGb;
                s.DiskUsagePercent = first.UsagePercent;
                s.DiskReadKbps = first.ReadKbps;
                s.DiskWriteKbps = first.WriteKbps;
            }
        }
        catch { }
    }

    private static (string name, long received, long sent) CollectNetworkData()
    {
        try
        {
            using var adapter = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus=2 AND PhysicalAdapter=True").Get().Cast<ManagementObject>().FirstOrDefault();
            if (adapter != null)
            {
                var name = adapter["Name"]?.ToString() ?? "Rede";
                using var stats = new ManagementObjectSearcher($"SELECT * FROM Win32_PerfFormattedData_Tcpip_NetworkInterface WHERE Name='{adapter["Name"]}'").Get().Cast<ManagementObject>().FirstOrDefault();
                if (stats != null)
                {
                    return (name, Convert.ToInt64(stats["BytesReceivedPersec"]), Convert.ToInt64(stats["BytesSentPersec"]));
                }
            }
        }
        catch { }
        return ("Rede", 0, 0);
    }

    private void CollectNetworkFromLhm(HardwareStats s)
    {
        try
        {
            var netHw = _lhm.GetHardware(HardwareType.Network).FirstOrDefault();
            if (netHw == null) return;

            s.NetworkName = string.IsNullOrEmpty(_cachedNetworkName)
                ? (netHw.Name ?? "Rede")
                : _cachedNetworkName;

            double totalReceived = 0, totalSent = 0;
            foreach (var sensor in netHw.Sensors)
            {
                if (!sensor.Value.HasValue) continue;
                var name = sensor.Name?.ToLowerInvariant() ?? "";
                if (name.Contains("download") || name.Contains("received"))
                    totalReceived += (double)sensor.Value;
                else if (name.Contains("upload") || name.Contains("sent"))
                    totalSent += (double)sensor.Value;
            }

            var now = DateTime.UtcNow;
            if (_lastLhmNetworkCheck != DateTime.MinValue)
            {
                var elapsed = (now - _lastLhmNetworkCheck).TotalSeconds;
                if (elapsed > 0)
                {
                    s.NetworkDownloadSpeed = Math.Max(0, (totalReceived - _lastLhmBytesReceived) / elapsed / 1024.0);
                    s.NetworkUploadSpeed = Math.Max(0, (totalSent - _lastLhmBytesSent) / elapsed / 1024.0);
                }
            }
            _lastLhmBytesReceived = totalReceived;
            _lastLhmBytesSent = totalSent;
            _lastLhmNetworkCheck = now;
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

    // ===== LHM Data Collection =====
    
    private void CollectFromLhm(HardwareStats s)
    {
        _lhm.Update();

        // CPU
        var cpu = _lhm.GetHardware(HardwareType.Cpu).FirstOrDefault();
        if (cpu != null)
        {
            s.CpuModel = cpu.Name;

            // Detect CPU architecture for sensor mapping (once)
            if (!_cpuDetected)
            {
                _cpuMapping.DetectCpuArchitecture(cpu.Name);
                _cpuDetected = true;
            }
            
            var cpuLoad = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("CPU Total"));
            if (cpuLoad != null) s.CpuUsage = cpuLoad.Value ?? 0;

            // Temperature - use mapping with fallbacks
            var tempSensorName = _cpuMapping.GetTempSensorName();
            var cpuTemp = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name == tempSensorName);
            
            // Try fallbacks if primary not found
            if (cpuTemp == null)
            {
                foreach (var fallback in _cpuMapping.GetTempFallbacks())
                {
                    cpuTemp = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name == fallback);
                    if (cpuTemp != null) break;
                }
            }
            
            // Generic fallback - any temperature sensor with value > 0
            if (cpuTemp == null)
                cpuTemp = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Value > 0);
            
            if (cpuTemp != null)
            {
                s.CpuTemp = Math.Round(cpuTemp.Value ?? 0, 1);
                s.CpuPackageTemp = s.CpuTemp; // Use same as temp for consistency
            }

            // Package temp (try to find separate package sensor)
            var pkgTemp = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Package"));
            if (pkgTemp != null) s.CpuPackageTemp = Math.Round(pkgTemp.Value ?? 0, 1);

            // Voltage - use mapping
            var voltageSensorName = _cpuMapping.GetVoltageSensorName();
            var vcore = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Voltage && s.Name == voltageSensorName);
            
            // Generic fallback
            if (vcore == null)
                vcore = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Voltage && s.Value > 0);
            
            if (vcore != null) s.CpuVoltage = Math.Round(vcore.Value ?? 0, 3);

            // Power - use mapping
            var powerSensorName = _cpuMapping.GetPowerSensorName();
            var cpuPower = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name == powerSensorName);
            
            // Generic fallback
            if (cpuPower == null)
                cpuPower = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power);
            
            if (cpuPower != null) s.CpuPower = Math.Round(cpuPower.Value ?? 0, 1);

            s.CpuCores = cpu.Sensors.Count(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Core"));
        }

        // GPU
        var gpu = _lhm.GetHardware(HardwareType.GpuNvidia)
            .Concat(_lhm.GetHardware(HardwareType.GpuAmd))
            .Concat(_lhm.GetHardware(HardwareType.GpuIntel))
            .FirstOrDefault();
        
        if (gpu != null)
        {
            s.GpuModel = gpu.Name;

            var gpuLoad = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load);
            if (gpuLoad != null) s.GpuUsage = gpuLoad.Value ?? 0;

            var gpuTemp = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Core"));
            if (gpuTemp == null) gpuTemp = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature);
            if (gpuTemp != null) s.GpuTemp = Math.Round(gpuTemp.Value ?? 0, 1);

            var gpuHotspot = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Hot Spot"));
            if (gpuHotspot != null) s.GpuHotspotTemp = Math.Round(gpuHotspot.Value ?? 0, 1);

            var gpuVoltage = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Voltage);
            if (gpuVoltage != null) s.GpuVoltage = Math.Round(gpuVoltage.Value ?? 0, 3);

            var gpuPower = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power);
            if (gpuPower != null) s.GpuPower = Math.Round(gpuPower.Value ?? 0, 1);
        }

        // Motherboard
        var mb = _lhm.GetHardware(HardwareType.Motherboard).FirstOrDefault();
        if (mb != null)
        {
            s.MotherboardModel = mb.Name;

            // Detect motherboard for mapping (once)
            if (!_motherboardDetected)
            {
                _mapping.DetectMotherboard(mb.Name);
                _motherboardDetected = true;
            }
            
            var mbTemps = mb.SubHardware
                .SelectMany(sub => sub.Sensors)
                .Where(s => s.SensorType == SensorType.Temperature && s.Value > 0 && s.Value < 120)
                .ToList();
            
            if (mbTemps.Count > 0)
            {
                var vrmTemp = mbTemps.FirstOrDefault(s => s.Name.Contains("VRM") || s.Name.Contains("PWM") || s.Name.Contains("MOS"));
                if (vrmTemp != null) s.MotherboardVrmTemp = Math.Round(vrmTemp.Value ?? 0, 1);

                var mbTemp = mbTemps.FirstOrDefault(s => !s.Name.Contains("VRM") && !s.Name.Contains("PWM") && !s.Name.Contains("MOS"));
                if (mbTemp != null) s.MotherboardTemp = Math.Round(mbTemp.Value ?? 0, 1);
            }

            var vcore = mb.SubHardware
                .SelectMany(sub => sub.Sensors)
                .FirstOrDefault(s => s.SensorType == SensorType.Voltage && s.Name.Contains("Vcore"));
            if (vcore != null) s.MotherboardVoltage = Math.Round(vcore.Value ?? 0, 3);
        }

        // Fans - collect from all hardware
        var fans = new List<FanInfo>();
        
        // GPU fans
        foreach (var hw in _lhm.GetHardware(HardwareType.GpuNvidia).Concat(_lhm.GetHardware(HardwareType.GpuAmd)))
        {
            foreach (var sensor in hw.Sensors.Where(s => s.SensorType == SensorType.Fan && s.Value > 0))
            {
                fans.Add(new FanInfo { Name = "GPU Fan", Rpm = sensor.Value ?? 0 });
            }
        }

        // Motherboard fans (from SuperIO/SubHardware)
        if (mb != null)
        {
            foreach (var sub in mb.SubHardware)
            {
                foreach (var sensor in sub.Sensors.Where(s => s.SensorType == SensorType.Fan && s.Value > 0))
                {
                    var mappedName = _mapping.MapFanName(sensor.Name);
                    fans.Add(new FanInfo { Name = mappedName, Rpm = sensor.Value ?? 0 });
                }
            }
        }

        if (fans.Count > 0)
            _cachedFans = fans;
    }

    // ===== Gamepad =====

    private List<GamepadInfo> _cachedGamepads = new();
    private DateTime _gamepadsCache = DateTime.MinValue;

    private List<GamepadInfo> CollectGamepadData()
    {
        if ((DateTime.UtcNow - _gamepadsCache).TotalSeconds < 5 && _cachedGamepads.Count > 0)
            return _cachedGamepads;

        _cachedGamepads = GamepadService.Collect();
        _gamepadsCache = DateTime.UtcNow;
        return _cachedGamepads;
    }

    // ===== Monitor Info =====

    private static List<MonitorInfo> _cachedMonitors = new();
    private static DateTime _monitorsCache = DateTime.MinValue;

    private static List<MonitorInfo> CollectMonitorData()
    {
        // Cache for 60 seconds (static data)
        if ((DateTime.UtcNow - _monitorsCache).TotalSeconds < 60 && _cachedMonitors.Count > 0)
            return _cachedMonitors;

        var monitors = new Dictionary<string, MonitorInfo>();

        try
        {
            // Step 1: EDID identification (name, manufacturer, serial)
            using (var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorID"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    var instance = obj["InstanceName"]?.ToString() ?? "";
                    var info = new MonitorInfo
                    {
                        Name = ConvertUshortToString(obj["UserFriendlyName"]),
                        Manufacturer = ConvertUshortToString(obj["ManufacturerName"]),
                        SerialNumber = ConvertUshortToString(obj["SerialNumberID"]),
                        ProductCode = ConvertUshortToString(obj["ProductCodeID"]),
                        YearOfManufacture = Convert.ToInt32(obj["YearOfManufacture"] ?? 0)
                    };
                    monitors[instance] = info;
                }
            }

            // Step 2: Physical dimensions and video input type
            using (var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorBasicDisplayParams"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    var instance = obj["InstanceName"]?.ToString() ?? "";
                    if (monitors.TryGetValue(instance, out var info))
                    {
                        info.WidthCm = Convert.ToInt32(obj["MaxHorizontalImageSize"] ?? 0);
                        info.HeightCm = Convert.ToInt32(obj["MaxVerticalImageSize"] ?? 0);
                        if (info.WidthCm > 0 && info.HeightCm > 0)
                            info.SizeInches = Math.Round(Math.Sqrt(info.WidthCm * info.WidthCm + info.HeightCm * info.HeightCm) / 2.54, 1);
                        info.ConnectionType = MapVideoInputType(Convert.ToInt32(obj["VideoInputType"] ?? 0));
                    }
                }
            }

            // Step 3: Connection params (more specific)
            using (var searcher = new ManagementObjectSearcher("root\\wmi", "SELECT * FROM WmiMonitorConnectionParams"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    var instance = obj["InstanceName"]?.ToString() ?? "";
                    if (monitors.TryGetValue(instance, out var info))
                    {
                        var connType = MapConnectionType(Convert.ToInt32(obj["VideoOutputTechnology"] ?? -1));
                        if (!string.IsNullOrEmpty(connType))
                            info.ConnectionType = connType;
                    }
                }
            }
        }
        catch { }

        // Step 4: Resolution/refresh from GPU
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            var gpuIdx = 0;
            foreach (ManagementObject gpu in searcher.Get())
            {
                var resW = Convert.ToInt32(gpu["CurrentHorizontalResolution"] ?? 0);
                var resH = Convert.ToInt32(gpu["CurrentVerticalResolution"] ?? 0);
                var refresh = Convert.ToInt32(gpu["CurrentRefreshRate"] ?? 0);
                var bpp = Convert.ToInt32(gpu["CurrentBitsPerPixel"] ?? 0);

                // Apply to matching monitor or create entry
                var monitorList = monitors.Values.ToList();
                if (gpuIdx < monitorList.Count)
                {
                    monitorList[gpuIdx].ResolutionWidth = resW;
                    monitorList[gpuIdx].ResolutionHeight = resH;
                    monitorList[gpuIdx].RefreshRate = refresh;
                    monitorList[gpuIdx].BitsPerPixel = bpp;
                }
                else if (resW > 0)
                {
                    // GPU without matching monitor entry
                    monitors[$"GPU_{gpuIdx}"] = new MonitorInfo
                    {
                        Name = gpu["Name"]?.ToString() ?? "Display",
                        ResolutionWidth = resW,
                        ResolutionHeight = resH,
                        RefreshRate = refresh,
                        BitsPerPixel = bpp
                    };
                }
                gpuIdx++;
            }
        }
        catch { }

        _cachedMonitors = monitors.Values.ToList();
        _monitorsCache = DateTime.UtcNow;
        return _cachedMonitors;
    }

    private static string ConvertUshortToString(object? arr)
    {
        if (arr is not ushort[] data) return "";
        return new string(data.Where(c => c != 0).Select(c => (char)c).ToArray());
    }

    private static string MapVideoInputType(int type) => type switch
    {
        1 => "Analog (VGA/DVI-A)",
        2 => "DVI-D",
        3 => "HDMI",
        4 => "DisplayPort",
        _ => "Other"
    };

    private static string MapConnectionType(int type) => type switch
    {
        2 => "DVI",
        3 => "HDMI",
        4 => "DisplayPort",
        5 => "DisplayPort (Embedded)",
        10 => "Internal Panel",
        _ => ""
    };

    // ===== Utility =====
    
    public void Dispose() { _fpsMonitor.Stop(); _lhm.Dispose(); GamepadService.Dispose(); }

    public void StartFpsMonitor(string processName) => _fpsMonitor.Start(processName);
    public void StopFpsMonitor() => _fpsMonitor.Stop();
}
