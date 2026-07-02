namespace EMECore.Core.Models;

public class HardwareStats
{
    public double CpuUsage { get; set; }
    public string CpuModel { get; set; } = "CPU";
    public double CpuTemp { get; set; }
    public double CpuPackageTemp { get; set; }
    public int CpuCores { get; set; }

    public double GpuUsage { get; set; }
    public string GpuModel { get; set; } = "GPU";
    public double GpuTemp { get; set; }

    public double TotalRam { get; set; }
    public double UsedRam { get; set; }
    public int RamSpeed { get; set; }

    public double DiskReadSpeed { get; set; }
    public double DiskWriteSpeed { get; set; }

    public List<FanInfo> Fans { get; set; } = new();
}

public class FanInfo
{
    public string Name { get; set; } = "";
    public double Rpm { get; set; }
    public double DutyPercent { get; set; }
}
