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
    public string? DetectedGame => _lastDetectedGame;
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

    // WMI network cache (refreshed every 5s to avoid WMI overhead)
    private double _cachedWmiNetDown, _cachedWmiNetUp;
    private DateTime _lastWmiNetCheck = DateTime.MinValue;
    private static readonly TimeSpan WmiNetRefreshInterval = TimeSpan.FromSeconds(5);

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

            CollectNetworkFromWmi(s);

            if (s.GpuUsage == 0 || s.GpuTemp == 0)
            {
                var (gpuLoad, gpuTemp) = ReadGpuViaSmi();
                if (gpuLoad > 0) s.GpuUsage = gpuLoad;
                if (gpuTemp > 0) s.GpuTemp = gpuTemp;
            }

            s.Monitors = CollectMonitorData();
        }

        // Network via LHM (fast, only when LHM data is fresh)
        if (refreshLhm)
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
        s.FpsLow1 = _fpsMonitor.Low1;
        s.FpsLow01 = _fpsMonitor.Low01;
        s.FpsFrameTimeMs = _fpsMonitor.FrameTimeMs;
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
        dst.CpuCoreLoads = new List<SensorReading>(src.CpuCoreLoads);
        dst.CpuCoreTemps = new List<SensorReading>(src.CpuCoreTemps);
        dst.CpuCoreClocks = new List<SensorReading>(src.CpuCoreClocks);
        dst.CpuManufacturer = src.CpuManufacturer;
        dst.CpuCoresPhysical = src.CpuCoresPhysical;
        dst.CpuThreads = src.CpuThreads;
        dst.CpuBaseClock = src.CpuBaseClock;
        dst.CpuMaxClock = src.CpuMaxClock;
        dst.CpuL2Cache = src.CpuL2Cache;
        dst.CpuL3Cache = src.CpuL3Cache;
        dst.CpuArchitecture = src.CpuArchitecture;
        dst.CpuSocket = src.CpuSocket;
        dst.GpuModel = src.GpuModel;
        dst.GpuUsage = src.GpuUsage;
        dst.GpuTemp = src.GpuTemp;
        dst.GpuHotspotTemp = src.GpuHotspotTemp;
        dst.GpuVoltage = src.GpuVoltage;
        dst.GpuPower = src.GpuPower;
        dst.GpuCoreClockMhz = src.GpuCoreClockMhz;
        dst.GpuMemoryClockMhz = src.GpuMemoryClockMhz;
        dst.GpuMemoryTotalMb = src.GpuMemoryTotalMb;
        dst.GpuMemoryUsedMb = src.GpuMemoryUsedMb;
        dst.GpuManufacturer = src.GpuManufacturer;
        dst.GpuDriverVersion = src.GpuDriverVersion;
        dst.GpuMemoryType = src.GpuMemoryType;
        dst.GpuClocks = new List<SensorReading>(src.GpuClocks);
        dst.GpuFanSpeeds = new List<SensorReading>(src.GpuFanSpeeds);
        dst.MotherboardModel = src.MotherboardModel;
        dst.MotherboardTemp = src.MotherboardTemp;
        dst.MotherboardVrmTemp = src.MotherboardVrmTemp;
        dst.MotherboardVoltage = src.MotherboardVoltage;
        dst.MotherboardChipsetTemp = src.MotherboardChipsetTemp;
        dst.MotherboardSocketTemp = src.MotherboardSocketTemp;
        dst.MotherboardPcieTemp = src.MotherboardPcieTemp;
        dst.Voltage12V = src.Voltage12V;
        dst.Voltage5V = src.Voltage5V;
        dst.Voltage33V = src.Voltage33V;
        dst.Voltage5Vsb = src.Voltage5Vsb;
        dst.VoltageDram = src.VoltageDram;
        dst.VoltageCmosBattery = src.VoltageCmosBattery;
        dst.VoltageCpuSoc = src.VoltageCpuSoc;
        dst.MbBiosVersion = src.MbBiosVersion;
        dst.MbBiosDate = src.MbBiosDate;
        dst.MbSerialNumber = src.MbSerialNumber;
        dst.MbTemperatures = new List<SensorReading>(src.MbTemperatures);
        dst.MbVoltages = new List<SensorReading>(src.MbVoltages);
        dst.MbFanDuties = new List<FanInfo>(src.MbFanDuties);
    }

    // ===== Game Detection =====

    private static readonly Dictionary<string, string[]> ProcessNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["skyrim_se"] = new[] { "SkyrimSE" },
        ["skyrim"] = new[] { "Skyrim" },
        ["oblivion_remastered"] = new[] { "OblivionRemastered" },
        ["teso"] = new[] { "eso64" },
        ["eldenring"] = new[] { "eldenring" },
        ["gta5"] = new[] { "GTA5", "PlayGTAV" },
        ["gta5_fivem"] = new[] { "FiveM" },
        ["cyberpunk2077"] = new[] { "Cyberpunk2077" },
        ["baldursgate3"] = new[] { "bg3" },
        ["red_dead_redemption_2"] = new[] { "RDR2" },
        ["the_witcher_3"] = new[] { "witcher3" },
        ["hogwarts_legacy"] = new[] { "HogwartsLegacy" },
        ["starfield"] = new[] { "Starfield" },
        ["palworld"] = new[] { "Palworld-Win64-Shipping" },
        ["blackmythwukong"] = new[] { "b1" },
        ["god_of_war"] = new[] { "GoW" },
        ["god_of_warragnarok"] = new[] { "GoWRagnarok" },
        ["monsterhunterworld"] = new[] { "MonsterHunterWorld" },
        ["monsterhunterrisen"] = new[] { "MonsterHunterRise" },
        ["monsterhunterwilds"] = new[] { "MonsterHunterWilds" },
        ["forza_horizon_5"] = new[] { "ForzaHorizon5" },
        ["forza_horizon_6"] = new[] { "ForzaHorizon6" },
        ["forza_motorsport"] = new[] { "ForzaMotorsport" },
        ["stellarblade"] = new[] { "SB-Win64-Shipping" },
        ["daysgone"] = new[] { "DaysGone" },
        ["subnautica"] = new[] { "Subnautica" },
        ["minecraft"] = new[] { "Minecraft" },
        ["valorant"] = new[] { "VALORANT" },
        ["cs2"] = new[] { "cs2" },
        ["apex_legends"] = new[] { "r5apex" },
        ["fortnite"] = new[] { "FortniteClient-Win64-Shipping" },
        ["league_of_legends"] = new[] { "League" },
        ["dota_2"] = new[] { "dota2" },
        ["overwatch_2"] = new[] { "Overwatch" },
        ["cyberpunk_phantom_liberty"] = new[] { "Cyberpunk2077" },
        ["star_wars_outlaws"] = new[] { "Outlaws" },
        ["avatar_frontiers"] = new[] { "Frontiers" },
        ["alan_wake_2"] = new[] { "AlanWake2" },
        ["silent_hill_2"] = new[] { "SH2" },
        ["resident_evil_4"] = new[] { "re4" },
        [" resident_evil_4_remake"] = new[] { "re4" },
        ["spider_man_miles_morales"] = new[] { "MilesMorales" },
        ["spider_man_remastered"] = new[] { "SpiderMan" },
        ["horizon_forbidden_west"] = new[] { "HorizonForbiddenWest" },
        ["ghost_of_tsushima"] = new[] { "GhostOfTsushima" },
        ["uncharted_legacy"] = new[] { "Uncharted" },
        ["the_last_of_us"] = new[] { "TheLastOfUs" },
        ["final_fantasy_vii_rebirth"] = new[] { "FF7Rebirth" },
        ["final_fantasy_xvi"] = new[] { "FFXVI" },
        ["wuthering_waves"] = new[] { "WutheringWaves" },
        ["genshin_impact"] = new[] { "GenshinImpact" },
        ["honkai_star_rail"] = new[] { "StarRail" },
        ["diablo_4"] = new[] { "Diablo IV" },
        ["path_of_exile_2"] = new[] { "PathOfExile" },
        ["manor_lords"] = new[] { "ManorLords" },
        ["likeness"] = new[] { "LIKENESS" },
        ["hogwarts_legacy"] = new[] { "HogwartsLegacy" },
    };

    public string? DetectRunningGame()
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo("tasklist", "/FO CSV /NH")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true });
            if (p == null) return null;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(2000);

            foreach (var line in output.Split('\n'))
            {
                var parts = line.Split(',');
                if (parts.Length < 1) continue;
                var procName = parts[0].Trim('"', ' ');
                if (procName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    procName = procName[..^4];

                foreach (var kvp in ProcessNameMap)
                {
                    foreach (var keyword in kvp.Value)
                    {
                        if (procName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                            return procName;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private void DetectGame()
    {
        try
        {
            var detected = DetectRunningGame();
            if (detected != null && detected != _lastDetectedGame)
                _lastDetectedGame = detected;
            else if (detected == null && _lastDetectedGame != null)
                _lastDetectedGame = null;
        }
        catch { }
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
                s.RamFree = Math.Round(Convert.ToDouble(os["FreePhysicalMemory"]) * 1024 / 1073741824, 1);
                if (os["TotalVirtualMemorySize"] != null)
                    s.RamVirtualTotal = Math.Round(Convert.ToDouble(os["TotalVirtualMemorySize"]) * 1024 / 1073741824, 1);
                if (os["FreeVirtualMemory"] != null)
                    s.RamVirtualUsed = Math.Round(s.RamVirtualTotal - Convert.ToDouble(os["FreeVirtualMemory"]) * 1024 / 1073741824, 1);
            }

            using var mem = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory").Get();
            var modules = mem.Cast<ManagementObject>().ToList();
            s.RamModules.Clear();
            if (modules.Count > 0)
            {
                var first = modules[0];
                s.RamModel = $"{first["Manufacturer"]} {first["PartNumber"]}".Trim();
                s.RamSpeed = Convert.ToInt32(first["ConfiguredClockSpeed"]);
                s.RamModuleCount = modules.Count;
                s.RamModuleSize = Math.Round(Convert.ToDouble(first["Capacity"]) / 1073741824, 0);
                if (first["ConfiguredVoltage"] != null)
                    s.RamVoltage = Math.Round(Convert.ToDouble(first["ConfiguredVoltage"]) / 1000.0, 1);
                if (first["MaxSpeed"] != null)
                    s.RamMaxSpeed = Convert.ToInt32(first["MaxSpeed"]);

                var memType = Convert.ToInt32(first["MemoryType"] ?? 0);
                s.RamType = memType switch
                {
                    20 => "DDR", 21 => "DDR2", 22 => "DDR2 FB-DIMM", 24 => "DDR3",
                    25 => "FBD2", 26 => "DDR4", 27 => "LPDDR", 28 => "LPDDR2",
                    29 => "LPDDR3", 30 => "LPDDR4", 31 => "DDR5", 32 => "LPDDR5",
                    _ => $"Tipo {memType}"
                };

                var ff = Convert.ToInt32(first["FormFactor"] ?? 0);
                s.RamFormFactor = ff switch
                {
                    8 => "DIMM", 10 => "SODIMM", 12 => "ROW", 13 => "LIP",
                    _ => $"FF {ff}"
                };

                for (int i = 0; i < modules.Count; i++)
                {
                    var m = modules[i];
                    var mod = new RamModuleInfo
                    {
                        Slot = m["DeviceLocator"]?.ToString() ?? $"Slot {i + 1}",
                        Manufacturer = m["Manufacturer"]?.ToString()?.Trim() ?? "",
                        PartNumber = m["PartNumber"]?.ToString()?.Trim() ?? "",
                        CapacityGb = Math.Round(Convert.ToDouble(m["Capacity"] ?? 0) / 1073741824, 0),
                        SpeedMHz = Convert.ToInt32(m["ConfiguredClockSpeed"] ?? 0),
                        MaxSpeedMHz = Convert.ToInt32(m["MaxSpeed"] ?? 0),
                        FormFactor = s.RamFormFactor,
                        MemoryType = s.RamType
                    };
                    if (m["ConfiguredVoltage"] != null)
                        mod.VoltageV = Math.Round(Convert.ToDouble(m["ConfiguredVoltage"]) / 1000.0, 1);
                    s.RamModules.Add(mod);
                }
            }
        }
        catch { }

        // WMI CPU detailed info
        try
        {
            using var cpuWmi = new ManagementObjectSearcher("SELECT * FROM Win32_Processor").Get().Cast<ManagementObject>().FirstOrDefault();
            if (cpuWmi != null)
            {
                s.CpuManufacturer = cpuWmi["Manufacturer"]?.ToString() ?? "";
                s.CpuCoresPhysical = Convert.ToInt32(cpuWmi["NumberOfCores"] ?? 0);
                s.CpuThreads = Convert.ToInt32(cpuWmi["NumberOfLogicalProcessors"] ?? 0);
                if (cpuWmi["MaxClockSpeed"] != null)
                    s.CpuMaxClock = Math.Round(Convert.ToDouble(cpuWmi["MaxClockSpeed"]) / 1000.0, 2);
                if (cpuWmi["CurrentClockSpeed"] != null)
                    s.CpuBaseClock = Math.Round(Convert.ToDouble(cpuWmi["CurrentClockSpeed"]) / 1000.0, 2);
                if (cpuWmi["L2CacheSize"] != null)
                    s.CpuL2Cache = Math.Round(Convert.ToDouble(cpuWmi["L2CacheSize"]) / 1024.0, 0);
                if (cpuWmi["L3CacheSize"] != null)
                    s.CpuL3Cache = Math.Round(Convert.ToDouble(cpuWmi["L3CacheSize"]) / 1024.0, 0);
                s.CpuSocket = cpuWmi["SocketDesignation"]?.ToString() ?? "";
                var arch = Convert.ToInt32(cpuWmi["Architecture"] ?? 0);
                s.CpuArchitecture = arch switch
                {
                    0 => "x86", 9 => "x64", 12 => "ARM64",
                    _ => $"Arch {arch}"
                };
            }
        }
        catch { }

        // WMI GPU detailed info
        try
        {
            using var gpuWmi = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController").Get().Cast<ManagementObject>().FirstOrDefault();
            if (gpuWmi != null)
            {
                s.GpuDriverVersion = gpuWmi["DriverVersion"]?.ToString() ?? "";
                if (gpuWmi["AdapterRAM"] != null)
                {
                    var vramBytes = Convert.ToDouble(gpuWmi["AdapterRAM"]);
                    if (s.GpuMemoryTotalMb <= 0 && vramBytes > 0)
                        s.GpuMemoryTotalMb = Math.Round(vramBytes / 1048576.0, 0);
                }
                s.GpuMemoryType = gpuWmi["VideoProcessor"]?.ToString() ?? "";
                var manufacturer = gpuWmi["AdapterCompatibility"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(manufacturer))
                    s.GpuManufacturer = manufacturer;
                else
                {
                    if (s.GpuModel.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) s.GpuManufacturer = "NVIDIA";
                    else if (s.GpuModel.Contains("AMD", StringComparison.OrdinalIgnoreCase) || s.GpuModel.Contains("Radeon", StringComparison.OrdinalIgnoreCase)) s.GpuManufacturer = "AMD";
                    else if (s.GpuModel.Contains("Intel", StringComparison.OrdinalIgnoreCase)) s.GpuManufacturer = "Intel";
                }
            }
        }
        catch { }

        // WMI Motherboard info (BIOS, serial)
        try
        {
            using var biosSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            foreach (ManagementObject obj in biosSearcher.Get())
            {
                s.MbBiosVersion = obj["SMBIOSBIOSVersion"]?.ToString() ?? obj["Version"]?.ToString() ?? "";
                s.MbBiosDate = obj["ReleaseDate"]?.ToString() ?? "";
                break;
            }
            using var boardSearcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (ManagementObject obj in boardSearcher.Get())
            {
                s.MbSerialNumber = obj["SerialNumber"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(s.MotherboardModel))
                    s.MotherboardModel = obj["Product"]?.ToString() ?? obj["Manufacturer"]?.ToString() ?? "";
                break;
            }
        }
        catch { }
    }

    private static void CollectDiskData(HardwareStats s)
    {
        try
        {
            s.Disks.Clear();
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
            using var searcher = new ManagementObjectSearcher("SELECT Name, BytesReceivedPersec, BytesSentPersec FROM Win32_PerfFormattedData_Tcpip_NetworkInterface WHERE BytesReceivedPersec IS NOT NULL");
            long totalReceived = 0, totalSent = 0;
            string name = "Rede";
            foreach (var obj in searcher.Get().Cast<ManagementObject>())
            {
                var n = obj["Name"]?.ToString();
                if (string.IsNullOrEmpty(n)) continue;
                if (n.Contains("Loopback") || n.Contains("isatap") || n.Contains("Teredo")) continue;

                if (name == "Rede") name = n;
                totalReceived += Convert.ToInt64(obj["BytesReceivedPersec"]);
                totalSent += Convert.ToInt64(obj["BytesSentPersec"]);
            }
            return (name, totalReceived, totalSent);
        }
        catch { }
        return ("Rede", 0, 0);
    }

    private void CollectNetworkFromLhm(HardwareStats s)
    {
        try
        {
            var adapters = _lhm.GetHardware(HardwareType.Network).ToList();
            if (adapters.Count == 0) return;

            IHardware? bestAdapter = null;
            double bestTraffic = -1;
            foreach (var hw in adapters)
            {
                double totalTraffic = 0;
                foreach (var sensor in hw.Sensors)
                {
                    if (!sensor.Value.HasValue) continue;
                    if (sensor.SensorType != SensorType.Data) continue;
                    totalTraffic += Math.Abs((double)sensor.Value);
                }
                if (totalTraffic > bestTraffic)
                {
                    bestTraffic = totalTraffic;
                    bestAdapter = hw;
                }
            }

            var netHw = bestAdapter ?? adapters.First();
            s.NetworkName = string.IsNullOrEmpty(_cachedNetworkName)
                ? (netHw.Name ?? "Rede")
                : _cachedNetworkName;

            double totalReceived = 0, totalSent = 0;
            bool hasSensor = false;
            foreach (var sensor in netHw.Sensors)
            {
                if (!sensor.Value.HasValue) continue;
                if (sensor.SensorType != SensorType.Data) continue;
                var name = sensor.Name?.ToLowerInvariant() ?? "";
                if (name.Contains("download") || name.Contains("received"))
                { totalReceived += (double)sensor.Value; hasSensor = true; }
                else if (name.Contains("upload") || name.Contains("sent"))
                { totalSent += (double)sensor.Value; hasSensor = true; }
            }

            if (hasSensor)
            {
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
        }
        catch { }
    }

    private void CollectNetworkFromWmi(HardwareStats s)
    {
        try
        {
            var now = DateTime.UtcNow;
            if (now - _lastWmiNetCheck > WmiNetRefreshInterval)
            {
                var (netName, received, sent) = CollectNetworkData();
                _cachedWmiNetDown = received / 1024.0;
                _cachedWmiNetUp = sent / 1024.0;
                if (string.IsNullOrEmpty(_cachedNetworkName))
                    _cachedNetworkName = string.IsNullOrEmpty(netName) ? "Rede" : netName;
                _lastWmiNetCheck = now;
            }
            s.NetworkName = string.IsNullOrEmpty(_cachedNetworkName) ? "Rede" : _cachedNetworkName;
            s.NetworkDownloadSpeed = _cachedWmiNetDown;
            s.NetworkUploadSpeed = _cachedWmiNetUp;
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
    
    private static int ExtractCoreNumber(string name)
    {
        var match = System.Text.RegularExpressions.Regex.Match(name, @"\d+");
        return match.Success ? int.Parse(match.Value) : 0;
    }

    private void CollectFromLhm(HardwareStats s)
    {
        _lhm.Update();

        // CPU
        var cpu = _lhm.GetHardware(HardwareType.Cpu).FirstOrDefault();
        if (cpu != null)
        {
            s.CpuModel = cpu.Name;

            if (!_cpuDetected)
            {
                _cpuMapping.DetectCpuArchitecture(cpu.Name);
                _cpuDetected = true;
            }
            
            var cpuLoad = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("CPU Total"));
            if (cpuLoad != null) s.CpuUsage = cpuLoad.Value ?? 0;

            var tempSensorName = _cpuMapping.GetTempSensorName();
            var cpuTemp = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name == tempSensorName);
            if (cpuTemp == null)
            {
                foreach (var fallback in _cpuMapping.GetTempFallbacks())
                {
                    cpuTemp = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Name == fallback);
                    if (cpuTemp != null) break;
                }
            }
            if (cpuTemp == null)
                cpuTemp = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature && s.Value > 0);
            if (cpuTemp != null)
                s.CpuTemp = Math.Round(cpuTemp.Value ?? 0, 1);

            var allTempSensors = cpu.Sensors.Where(s => s.SensorType == SensorType.Temperature).ToList();
            var coreSensors = allTempSensors
                .Where(s => s.Value > 0
                    && (s.Name.Contains("CCD") || s.Name.StartsWith("Core #"))
                    && !s.Name.Contains("Tctl"))
                .ToList();
            if (coreSensors.Count > 0)
            {
                var maxCore = coreSensors.Max(s => s.Value ?? 0);
                s.CpuTemp = Math.Round(maxCore, 1);
            }

            var pkgTemp = allTempSensors.FirstOrDefault(s => s.Name.Contains("Tctl"));
            if (pkgTemp == null)
                pkgTemp = allTempSensors.FirstOrDefault(s => s.Name.Contains("Package"));
            if (pkgTemp != null)
                s.CpuPackageTemp = Math.Round(pkgTemp.Value ?? 0, 1);
            else
                s.CpuPackageTemp = s.CpuTemp;

            var voltageSensorName = _cpuMapping.GetVoltageSensorName();
            var vcore = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Voltage && s.Name == voltageSensorName);
            if (vcore == null)
                vcore = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Voltage && s.Value > 0);
            if (vcore != null) s.CpuVoltage = Math.Round(vcore.Value ?? 0, 3);

            var powerSensorName = _cpuMapping.GetPowerSensorName();
            var cpuPower = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power && s.Name == powerSensorName);
            if (cpuPower == null)
                cpuPower = cpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Power);
            if (cpuPower != null) s.CpuPower = Math.Round(cpuPower.Value ?? 0, 1);

            s.CpuCores = cpu.Sensors.Count(s => s.SensorType == SensorType.Temperature && s.Name.Contains("Core"));

            // Per-core loads
            var perCoreLoads = cpu.Sensors
                .Where(s => s.SensorType == SensorType.Load && s.Name.StartsWith("CPU Core"))
                .OrderBy(s => ExtractCoreNumber(s.Name))
                .ToList();
            foreach (var cl in perCoreLoads)
                s.CpuCoreLoads.Add(new SensorReading { Name = cl.Name, Value = Math.Round(cl.Value ?? 0, 0), Unit = "%" });

            // Per-core temps
            var perCoreTemps = cpu.Sensors
                .Where(s => s.SensorType == SensorType.Temperature
                    && (s.Name.StartsWith("Core #") || s.Name.Contains("CCD"))
                    && s.Value > 0)
                .OrderBy(s => ExtractCoreNumber(s.Name))
                .ToList();
            foreach (var ct in perCoreTemps)
                s.CpuCoreTemps.Add(new SensorReading { Name = ct.Name, Value = Math.Round(ct.Value ?? 0, 1), Unit = "°C" });

            // Per-core clocks
            var allClockSensors = cpu.Sensors
                .Where(s => s.SensorType == SensorType.Clock && s.Value > 0)
                .OrderBy(s => s.Name)
                .ToList();
            var perCoreClocks = allClockSensors
                .Where(s => s.Name.StartsWith("CPU Core"))
                .OrderBy(s => ExtractCoreNumber(s.Name))
                .ToList();
            if (perCoreClocks.Count == 0 && allClockSensors.Count > 0)
            {
                foreach (var ck in allClockSensors)
                    s.CpuCoreClocks.Add(new SensorReading { Name = ck.Name, Value = Math.Round(ck.Value ?? 0, 0), Unit = "MHz" });
            }
            else
            {
                foreach (var ck in perCoreClocks)
                    s.CpuCoreClocks.Add(new SensorReading { Name = ck.Name, Value = Math.Round(ck.Value ?? 0, 0), Unit = "MHz" });
            }
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

            // GPU clocks
            var gpuCoreClock = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core"));
            if (gpuCoreClock != null) s.GpuCoreClockMhz = Math.Round(gpuCoreClock.Value ?? 0, 0);
            var gpuMemClock = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Clock && s.Name.Contains("Memory"));
            if (gpuMemClock != null) s.GpuMemoryClockMhz = Math.Round(gpuMemClock.Value ?? 0, 0);

            // GPU memory used/total
            var gpuMemUsed = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("GPU Memory Used"));
            if (gpuMemUsed != null) s.GpuMemoryUsedMb = Math.Round(gpuMemUsed.Value ?? 0, 0);
            var gpuMemTotal = gpu.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Data && s.Name.Contains("GPU Memory Total"));
            if (gpuMemTotal != null) s.GpuMemoryTotalMb = Math.Round(gpuMemTotal.Value ?? 0, 0);

            // GPU fan speeds
            var gpuFans = gpu.Sensors.Where(s => s.SensorType == SensorType.Fan).ToList();
            foreach (var fan in gpuFans)
                s.GpuFanSpeeds.Add(new SensorReading { Name = fan.Name, Value = Math.Round(fan.Value ?? 0, 0), Unit = "RPM" });

            // All GPU clocks
            var gpuClocks = gpu.Sensors.Where(s => s.SensorType == SensorType.Clock && s.Value > 0).ToList();
            foreach (var clk in gpuClocks)
                s.GpuClocks.Add(new SensorReading { Name = clk.Name, Value = Math.Round(clk.Value ?? 0, 0), Unit = "MHz" });
        }

        // Motherboard
        var mb = _lhm.GetHardware(HardwareType.Motherboard).FirstOrDefault();
        if (mb != null)
        {
            s.MotherboardModel = mb.Name;

            if (!_motherboardDetected)
            {
                _mapping.DetectMotherboard(mb.Name);
                _motherboardDetected = true;
            }
            
            // ALL temperatures from SuperIO
            var allMbTemps = mb.SubHardware
                .SelectMany(sub => sub.Sensors)
                .Where(s => s.SensorType == SensorType.Temperature && s.Value > 0 && s.Value < 120)
                .ToList();

            foreach (var t in allMbTemps)
            {
                var name = _mapping.MapTemperatureName(t.Name);
                s.MbTemperatures.Add(new SensorReading { Name = name, Value = Math.Round(t.Value ?? 0, 1), Unit = "°C" });

                if (name.Contains("VRM") || name.Contains("PWM") || name.Contains("MOS"))
                    s.MotherboardVrmTemp = Math.Round(t.Value ?? 0, 1);
                else if (name.Contains("Chipset") || name.Contains("PCH"))
                    s.MotherboardChipsetTemp = Math.Round(t.Value ?? 0, 1);
                else if (name.Contains("Socket") || name.Contains("CPU"))
                    s.MotherboardSocketTemp = Math.Round(t.Value ?? 0, 1);
                else if (name.Contains("PCI"))
                    s.MotherboardPcieTemp = Math.Round(t.Value ?? 0, 1);
                else if (s.MotherboardTemp == 0)
                    s.MotherboardTemp = Math.Round(t.Value ?? 0, 1);
            }

            // ALL voltages from SuperIO
            var allMbVoltages = mb.SubHardware
                .SelectMany(sub => sub.Sensors)
                .Where(s => s.SensorType == SensorType.Voltage && s.Value > 0)
                .ToList();

            foreach (var v in allMbVoltages)
            {
                var name = _mapping.MapVoltageName(v.Name);
                var val = Math.Round(v.Value ?? 0, 3);
                s.MbVoltages.Add(new SensorReading { Name = name, Value = val, Unit = "V" });

                if (name.Contains("Vcore"))
                    s.MotherboardVoltage = val;
                else if (name.Contains("+12V") || name.Contains("12V"))
                    s.Voltage12V = val;
                else if (name.Contains("+5V") && !name.Contains("SB"))
                    s.Voltage5V = val;
                else if (name.Contains("+3.3V") || name.Contains("3VCC") || name.Contains("AVCC"))
                    s.Voltage33V = val;
                else if (name.Contains("5VSB") || name.Contains("Standby"))
                    s.Voltage5Vsb = val;
                else if (name.Contains("DRAM") || name.Contains("DIMM"))
                    s.VoltageDram = val;
                else if (name.Contains("CMOS") || name.Contains("Battery") || name.Contains("VBAT"))
                    s.VoltageCmosBattery = val;
                else if (name.Contains("SoC") || name.Contains("NB"))
                    s.VoltageCpuSoc = val;
            }

            // Fan duty cycles
            foreach (var sub in mb.SubHardware)
            {
                foreach (var ctrl in sub.Sensors.Where(s => s.SensorType == SensorType.Control))
                {
                    var dutyName = ctrl.Name.Replace("Control for ", "").Replace("Duty Cycle# ", "Fan ");
                    s.MbFanDuties.Add(new FanInfo
                    {
                        Name = _mapping.MapFanName(dutyName),
                        DutyPercent = Math.Round(ctrl.Value ?? 0, 1)
                    });
                }
            }
        }

        // Fans - collect from all hardware
        var fans = new List<FanInfo>();
        
        foreach (var hw in _lhm.GetHardware(HardwareType.GpuNvidia).Concat(_lhm.GetHardware(HardwareType.GpuAmd)))
        {
            foreach (var sensor in hw.Sensors.Where(s => s.SensorType == SensorType.Fan && s.Value > 0))
            {
                fans.Add(new FanInfo { Name = "GPU Fan", Rpm = sensor.Value ?? 0 });
            }
        }

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
        // Match using Win32_PnPEntity to determine the correct order
        try
        {
            // Collect GPU data
            var gpuDataList = new List<(string pnpId, int resW, int resH, int refresh, int bpp)>();
            using (var gpuSearcher = new ManagementObjectSearcher("SELECT PNPDeviceID, CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate, CurrentBitsPerPixel FROM Win32_VideoController"))
            {
                foreach (ManagementObject gpu in gpuSearcher.Get())
                {
                    gpuDataList.Add((
                        gpu["PNPDeviceID"]?.ToString() ?? "",
                        Convert.ToInt32(gpu["CurrentHorizontalResolution"] ?? 0),
                        Convert.ToInt32(gpu["CurrentVerticalResolution"] ?? 0),
                        Convert.ToInt32(gpu["CurrentRefreshRate"] ?? 0),
                        Convert.ToInt32(gpu["CurrentBitsPerPixel"] ?? 0)
                    ));
                }
            }

            // Build EDID lookup by vendor+product code (e.g. "DEL4150")
            var edidByCode = new Dictionary<string, MonitorInfo>();
            foreach (var kvp in monitors)
            {
                var instParts = kvp.Key.Split('\\');
                if (instParts.Length >= 2)
                    edidByCode[instParts[1]] = kvp.Value;
            }

            // Get ordered list from Win32_PnPEntity (deterministic device tree order)
            var orderedMonitors = new List<MonitorInfo>();
            try
            {
                using var pnpSearcher = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_PnPEntity WHERE PNPClass = 'Monitor'");
                foreach (ManagementObject pnp in pnpSearcher.Get())
                {
                    var devId = pnp["DeviceID"]?.ToString() ?? "";
                    var parts = devId.Split('\\');
                    if (parts.Length >= 2 && edidByCode.TryGetValue(parts[1], out var info))
                    {
                        if (!orderedMonitors.Contains(info))
                            orderedMonitors.Add(info);
                    }
                }
            }
            catch { }

            // Add any remaining EDID monitors not found by PnP
            foreach (var info in monitors.Values)
            {
                if (!orderedMonitors.Contains(info))
                    orderedMonitors.Add(info);
            }

            // Match by index: PnP order should match GPU order
            for (int i = 0; i < orderedMonitors.Count && i < gpuDataList.Count; i++)
            {
                orderedMonitors[i].ResolutionWidth = gpuDataList[i].resW;
                orderedMonitors[i].ResolutionHeight = gpuDataList[i].resH;
                orderedMonitors[i].RefreshRate = gpuDataList[i].refresh;
                orderedMonitors[i].BitsPerPixel = gpuDataList[i].bpp;
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
    public FpsMonitorService FpsMonitor => _fpsMonitor;
}
