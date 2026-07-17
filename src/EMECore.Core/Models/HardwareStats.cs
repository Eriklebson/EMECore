namespace EMECore.Core.Models;

public class HardwareStats
{
    // ===== Motherboard =====
    public string MotherboardModel { get; set; } = "";
    public double MotherboardTemp { get; set; }
    public double MotherboardVrmTemp { get; set; }
    public double MotherboardVoltage { get; set; }
    public double MotherboardChipsetTemp { get; set; }
    public double MotherboardSocketTemp { get; set; }
    public double MotherboardPcieTemp { get; set; }
    public double Voltage12V { get; set; }
    public double Voltage5V { get; set; }
    public double Voltage33V { get; set; }
    public double Voltage5Vsb { get; set; }
    public double VoltageDram { get; set; }
    public double VoltageCmosBattery { get; set; }
    public double VoltageCpuSoc { get; set; }
    public string MbBiosVersion { get; set; } = "";
    public string MbBiosDate { get; set; } = "";
    public string MbSerialNumber { get; set; } = "";
    public List<SensorReading> MbTemperatures { get; set; } = new();
    public List<SensorReading> MbVoltages { get; set; } = new();
    public List<FanInfo> MbFanDuties { get; set; } = new();

    // ===== CPU =====
    public double CpuUsage { get; set; }
    public string CpuModel { get; set; } = "CPU";
    public double CpuTemp { get; set; }
    public double CpuPackageTemp { get; set; }
    public double CpuVoltage { get; set; }
    public double CpuPower { get; set; }
    public int CpuCores { get; set; }
    public string CpuManufacturer { get; set; } = "";
    public int CpuCoresPhysical { get; set; }
    public int CpuThreads { get; set; }
    public double CpuBaseClock { get; set; }
    public double CpuMaxClock { get; set; }
    public double CpuL2Cache { get; set; }
    public double CpuL3Cache { get; set; }
    public string CpuArchitecture { get; set; } = "";
    public string CpuSocket { get; set; } = "";
    public List<SensorReading> CpuCoreLoads { get; set; } = new();
    public List<SensorReading> CpuCoreTemps { get; set; } = new();
    public List<SensorReading> CpuCoreClocks { get; set; } = new();

    // ===== GPU =====
    public double GpuUsage { get; set; }
    public string GpuModel { get; set; } = "GPU";
    public double GpuTemp { get; set; }
    public double GpuHotspotTemp { get; set; }
    public double GpuVoltage { get; set; }
    public double GpuPower { get; set; }
    public double GpuCoreClockMhz { get; set; }
    public double GpuMemoryClockMhz { get; set; }
    public double GpuMemoryTotalMb { get; set; }
    public double GpuMemoryUsedMb { get; set; }
    public string GpuManufacturer { get; set; } = "";
    public string GpuDriverVersion { get; set; } = "";
    public string GpuMemoryType { get; set; } = "";
    public List<SensorReading> GpuClocks { get; set; } = new();
    public List<SensorReading> GpuFanSpeeds { get; set; } = new();

    // ===== RAM =====
    public double TotalRam { get; set; }
    public double UsedRam { get; set; }
    public double RamFree { get; set; }
    public int RamSpeed { get; set; }
    public string RamModel { get; set; } = "";
    public int RamModuleCount { get; set; }
    public double RamModuleSize { get; set; }
    public double RamVoltage { get; set; }
    public int RamMaxSpeed { get; set; }
    public string RamType { get; set; } = "";
    public string RamFormFactor { get; set; } = "";
    public double RamVirtualTotal { get; set; }
    public double RamVirtualUsed { get; set; }
    public List<RamModuleInfo> RamModules { get; set; } = new();

    // ===== FPS =====
    public double FpsCurrent { get; set; }
    public double FpsMin { get; set; }
    public double FpsMax { get; set; }
    public double FpsAvg { get; set; }
    public double FpsLow1 { get; set; }
    public double FpsLow01 { get; set; }
    public double FpsFrameTimeMs { get; set; }
    public string FpsSource { get; set; } = "Off";

    // ===== Disk =====
    public double DiskReadSpeed { get; set; }
    public double DiskWriteSpeed { get; set; }
    public string DiskName { get; set; } = "";
    public double DiskUsedGb { get; set; }
    public double DiskTotalGb { get; set; }
    public double DiskUsagePercent { get; set; }
    public double DiskReadKbps { get; set; }
    public double DiskWriteKbps { get; set; }

    // ===== Network =====
    public double NetworkDownloadSpeed { get; set; }
    public double NetworkUploadSpeed { get; set; }
    public string NetworkName { get; set; } = "";

    // ===== Collections =====
    public List<FanInfo> Fans { get; set; } = new();
    public List<DiskInfo> Disks { get; set; } = new();
    public List<MonitorInfo> Monitors { get; set; } = new();
    public List<GamepadInfo> Gamepads { get; set; } = new();
}

public class SensorReading
{
    public string Name { get; set; } = "";
    public double Value { get; set; }
    public string Unit { get; set; } = "";
}

public class RamModuleInfo
{
    public string Slot { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string PartNumber { get; set; } = "";
    public double CapacityGb { get; set; }
    public int SpeedMHz { get; set; }
    public int MaxSpeedMHz { get; set; }
    public string FormFactor { get; set; } = "";
    public string MemoryType { get; set; } = "";
    public double VoltageV { get; set; }
}

public class FanInfo
{
    public string Name { get; set; } = "";
    public double Rpm { get; set; }
    public double DutyPercent { get; set; }
}

public class DiskInfo
{
    public string DeviceId { get; set; } = "";
    public string Model { get; set; } = "";
    public string VolumeName { get; set; } = "";
    public string FileSystem { get; set; } = "";
    public int DriveType { get; set; }
    public double TotalGb { get; set; }
    public double UsedGb { get; set; }
    public double FreeGb { get; set; }
    public double UsagePercent { get; set; }
    public double ReadKbps { get; set; }
    public double WriteKbps { get; set; }
    public string InterfaceType { get; set; } = "";
    public string MediaType { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public int Partitions { get; set; }
    public double PercentTime { get; set; }
    public double PercentIdle { get; set; }
}
