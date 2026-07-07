using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace EMECore.Hardware.Models;

public class HardwareMappingConfig
{
    [JsonPropertyName("Motherboards")]
    public List<MotherboardMapping> Motherboards { get; set; } = new();

    [JsonPropertyName("DefaultMapping")]
    public DefaultMappingConfig DefaultMapping { get; set; } = new();
}

public class MotherboardMapping
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Chip")]
    public string Chip { get; set; } = string.Empty;

    [JsonPropertyName("FanMapping")]
    public Dictionary<string, string> FanMapping { get; set; } = new();

    [JsonPropertyName("TemperatureMapping")]
    public Dictionary<string, string> TemperatureMapping { get; set; } = new();

    [JsonPropertyName("VoltageMapping")]
    public Dictionary<string, string> VoltageMapping { get; set; } = new();

    [JsonPropertyName("FanCategories")]
    public FanCategories FanCategories { get; set; } = new();
}

public class FanCategories
{
    [JsonPropertyName("CPU")]
    public List<string> CPU { get; set; } = new();

    [JsonPropertyName("GPU")]
    public List<string> GPU { get; set; } = new();

    [JsonPropertyName("Motherboard")]
    public List<string> Motherboard { get; set; } = new();
}

public class DefaultMappingConfig
{
    [JsonPropertyName("FanCategory")]
    public string FanCategory { get; set; } = "Motherboard";
}
