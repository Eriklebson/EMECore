namespace EMECore.Core.Models;

public class MonitorInfo
{
    public string Name { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public int YearOfManufacture { get; set; }
    public int WidthCm { get; set; }
    public int HeightCm { get; set; }
    public double SizeInches { get; set; }
    public string ConnectionType { get; set; } = "";
    public int ResolutionWidth { get; set; }
    public int ResolutionHeight { get; set; }
    public int RefreshRate { get; set; }
    public int BitsPerPixel { get; set; }
}
