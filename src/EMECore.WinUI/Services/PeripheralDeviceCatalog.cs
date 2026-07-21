using System.Text.Json;

namespace EMECore.WinUI.Services;

internal sealed class PeripheralDeviceDefinition
{
    public string VendorId { get; set; } = "";
    public string ProductId { get; set; } = "";
    public string[] ProductIds { get; set; } = Array.Empty<string>();
    public string Name { get; set; } = "Periférico";
    public string Type { get; set; } = "generic";
    public string Provider { get; set; } = "windows-battery";
    public ushort UsagePage { get; set; }
    public ushort UsageId { get; set; }
    public byte DeviceIndex { get; set; } = 0xFF;
    public ushort BatteryFeature { get; set; }
    public string BatteryCommand { get; set; } = "";
    public double[] BatteryPolynomial { get; set; } = Array.Empty<double>();
    public int[] BatteryPercentages { get; set; } = Array.Empty<int>();
    public int[] BatteryVoltages { get; set; } = Array.Empty<int>();
    public int MinimumVoltage { get; set; } = 3000;
    public int MaximumVoltage { get; set; } = 5000;
    public bool SupportsPollingRate { get; set; }
    public int FixedPollingRateHz { get; set; }
    public bool Enabled { get; set; } = true;
    public string SupportStatus { get; set; } = "active";
    public string Source { get; set; } = "";

    public bool Matches(string devicePath) =>
        devicePath.Contains($"VID_{VendorId}", StringComparison.OrdinalIgnoreCase) &&
        (!string.IsNullOrWhiteSpace(ProductId) &&
         devicePath.Contains($"PID_{ProductId}", StringComparison.OrdinalIgnoreCase) ||
         ProductIds.Any(productId =>
             devicePath.Contains($"PID_{productId}", StringComparison.OrdinalIgnoreCase)));
}

internal static class PeripheralDeviceCatalog
{
    private static readonly Lazy<IReadOnlyList<PeripheralDeviceDefinition>> Definitions = new(Load);

    public static IReadOnlyList<PeripheralDeviceDefinition> All => Definitions.Value;

    private static IReadOnlyList<PeripheralDeviceDefinition> Load()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "config", "peripheral-devices.json");
            if (!File.Exists(path)) return Array.Empty<PeripheralDeviceDefinition>();

            var json = File.ReadAllText(path);
            var definitions = JsonSerializer.Deserialize<List<PeripheralDeviceDefinition>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return definitions ?? (IReadOnlyList<PeripheralDeviceDefinition>)
                Array.Empty<PeripheralDeviceDefinition>();
        }
        catch
        {
            return Array.Empty<PeripheralDeviceDefinition>();
        }
    }
}
