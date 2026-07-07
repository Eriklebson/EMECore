namespace EMECore.Core.Models;

public class HardwareStats
{
    public string MotherboardModel { get; set; } = "";
    public double MotherboardTemp { get; set; }
    public double MotherboardVrmTemp { get; set; }
    public double MotherboardVoltage { get; set; }

    public double CpuUsage { get; set; }
    public string CpuModel { get; set; } = "CPU";
    public double CpuTemp { get; set; }
    public double CpuPackageTemp { get; set; }
    public double CpuVoltage { get; set; }
    public double CpuPower { get; set; }
    public int CpuCores { get; set; }

    public double GpuUsage { get; set; }
    public string GpuModel { get; set; } = "GPU";
    public double GpuTemp { get; set; }
    public double GpuHotspotTemp { get; set; }
    public double GpuVoltage { get; set; }
    public double GpuPower { get; set; }

    public double TotalRam { get; set; }
    public double UsedRam { get; set; }
    public int RamSpeed { get; set; }
    public string RamModel { get; set; } = "";
    public int RamModuleCount { get; set; }
    public double RamModuleSize { get; set; }
    public double RamVoltage { get; set; }

    public double FpsCurrent { get; set; }
    public double FpsMin { get; set; }
    public double FpsMax { get; set; }
    public double FpsAvg { get; set; }
    public string FpsSource { get; set; } = "Off";

    public double DiskReadSpeed { get; set; }
    public double DiskWriteSpeed { get; set; }
    public string DiskName { get; set; } = "";
    public double DiskUsedGb { get; set; }
    public double DiskTotalGb { get; set; }
    public double DiskUsagePercent { get; set; }
    public double DiskReadKbps { get; set; }
    public double DiskWriteKbps { get; set; }

    public double NetworkDownloadSpeed { get; set; }
    public double NetworkUploadSpeed { get; set; }
    public string NetworkName { get; set; } = "";

    public List<FanInfo> Fans { get; set; } = new();
    public List<DiskInfo> Disks { get; set; } = new();
    public List<MonitorInfo> Monitors { get; set; } = new();
    public List<GamepadInfo> Gamepads { get; set; } = new();
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
}
