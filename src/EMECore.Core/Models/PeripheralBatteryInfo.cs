namespace EMECore.Core.Models;

public class PeripheralBatteryInfo
{
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public int BatteryPercent { get; set; } = -1;
    public string Status { get; set; } = "Desconhecido";
    public int PollingRateHz { get; set; }
    public bool SupportsPollingRate { get; set; }
}
